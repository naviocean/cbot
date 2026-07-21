using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace RedWave.Common
{
    public class CTrailingManager
    {
        private Robot _robot;
        private Symbol _symbol;
        private string _label;
        private CLogger _logger;

        // Trailing settings (in Pips)
        private bool _useTrailing;
        private double _trailStartPips;
        private double _trailStepPips;
        private double _trailSensitivityPips;

        // Break-even settings (in Pips)
        private bool _useBreakeven;
        private double _beStartPips;
        private double _beLockPips;
        private bool _beAddSpread;

        // Tracking state for positions
        private class PositionTrack
        {
            public int Id { get; set; }
            public bool BreakEvenDone { get; set; }
            public bool TrailingStarted { get; set; }
            /// <summary>
            /// Absolute profit (pips) required before trailing starts for this position.
            /// 0 = use global _trailStartPips from entry.
            /// After TP1 partial, set to currentPips + extra so we don't trail-stop immediately.
            /// </summary>
            public double ActivateAtPips { get; set; }
        }

        private List<PositionTrack> _trackedPositions;

        public CTrailingManager()
        {
            _robot = null;
            _symbol = null;
            _label = "";
            _logger = null;

            _useTrailing = false;
            _useBreakeven = false;
            _trackedPositions = new List<PositionTrack>();
        }

        public bool Init(Robot robot, Symbol symbol, string label, CLogger logger = null)
        {
            _logger = logger;
            if (robot == null || symbol == null)
            {
                _logger?.Error("TrailingManager: Initialization failed. Robot or Symbol is null.");
                return false;
            }
            _robot = robot;
            _symbol = symbol;
            _label = label;
            return true;
        }

        public void SetTrailPoints(double startPips, double stepPips, double sensitivityPips)
        {
            _useTrailing = true;
            _trailStartPips = startPips;
            _trailStepPips = stepPips;
            _trailSensitivityPips = sensitivityPips;
        }

        /// <summary>
        /// Arm trailing for a live position so it only starts after
        /// <paramref name="extraStartPips"/> more profit from the current level
        /// (not from entry — avoids instant trail-stop after a large TP1 move).
        /// </summary>
        public void ArmTrailFromCurrent(int positionId, double currentProfitPips, double extraStartPips, double stepPips, double sensitivityPips)
        {
            _useTrailing = true;
            _trailStartPips = extraStartPips;
            _trailStepPips = stepPips;
            _trailSensitivityPips = sensitivityPips;

            var track = _trackedPositions.FirstOrDefault(t => t.Id == positionId);
            if (track == null)
            {
                track = new PositionTrack { Id = positionId };
                _trackedPositions.Add(track);
            }

            track.ActivateAtPips = currentProfitPips + Math.Max(0, extraStartPips);
            track.TrailingStarted = false;
            _logger?.Debug($"TrailingManager: Arm #{positionId} activateAt={track.ActivateAtPips:F0} pips (now={currentProfitPips:F0}+{extraStartPips:F0})");
        }

        public void SetBreakevenPoints(double triggerPips, double lockPips, bool addSpread)
        {
            _useBreakeven = true;
            _beStartPips = triggerPips;
            _beLockPips = lockPips;
            _beAddSpread = addSpread;
        }

        public void OnTick()
        {
            if (_robot == null || _symbol == null) return;

            // Find all active positions belonging to this bot
            var activePositions = _robot.Positions.FindAll(_label, _symbol.Name);

            // Cleanup tracked positions that are no longer active
            _trackedPositions.RemoveAll(t => !activePositions.Any(p => p.Id == t.Id));

            foreach (var position in activePositions)
            {
                // Ensure position is tracked
                var track = _trackedPositions.FirstOrDefault(t => t.Id == position.Id);
                if (track == null)
                {
                    track = new PositionTrack
                    {
                        Id = position.Id,
                        BreakEvenDone = false,
                        TrailingStarted = false,
                        ActivateAtPips = 0
                    };
                    _trackedPositions.Add(track);
                }

                // 1. Manage Break-even
                if (_useBreakeven && !track.BreakEvenDone)
                {
                    ManageBreakeven(position, track);
                }

                // 2. Manage Trailing Stop
                if (_useTrailing)
                {
                    ManageTrailing(position, track);
                }
            }
        }

        private double GetMinStopDistancePrice()
        {
            if (_symbol == null) return 0;
            if (_symbol.MinDistanceType == SymbolMinDistanceType.Pips)
            {
                return PriceUtils.PipsToPrice(_symbol.MinStopLossDistance, _symbol);
            }
            else if (_symbol.MinDistanceType == SymbolMinDistanceType.Percentage)
            {
                double midPrice = (_symbol.Bid + _symbol.Ask) / 2.0;
                return midPrice * (_symbol.MinStopLossDistance / 100.0);
            }
            return 0;
        }

        private void ManageBreakeven(Position position, PositionTrack track)
        {
            double profitPips = position.Pips;
            if (profitPips < _beStartPips) return;

            double extraPips = _beAddSpread ? PriceUtils.PriceToPips(_symbol.Spread, _symbol) : 0;
            double lockPriceOffset = PriceUtils.PipsToPrice(_beLockPips + extraPips, _symbol);

            double? newSL = null;

            // Calculate minimum allowed distance for Stop Loss
            double minDist = GetMinStopDistancePrice();

            if (position.TradeType == TradeType.Buy)
            {
                newSL = PriceUtils.NormalizePrice(position.EntryPrice + lockPriceOffset, _symbol);
                
                // Clamp by MinStopLossDistance to avoid invalid SL errors
                double maxAllowedSL = PriceUtils.NormalizePrice(_symbol.Bid - minDist, _symbol);
                if (newSL > maxAllowedSL)
                {
                    newSL = maxAllowedSL;
                }

                if (position.StopLoss == null || newSL > position.StopLoss)
                {
                    _logger?.Info($"TrailingManager: Triggering Break-even for BUY #{position.Id}. New SL: {newSL}");
                    var result = _robot.ModifyPosition(position, newSL, position.TakeProfit, ProtectionType.Absolute);
                    if (result.IsSuccessful)
                    {
                        track.BreakEvenDone = true;
                    }
                    else
                    {
                        LogModifyFail(position.Id, "Break-even BUY", result.Error);
                    }
                }
            }
            else if (position.TradeType == TradeType.Sell)
            {
                newSL = PriceUtils.NormalizePrice(position.EntryPrice - lockPriceOffset, _symbol);

                // Clamp by MinStopLossDistance to avoid invalid SL errors
                double minAllowedSL = PriceUtils.NormalizePrice(_symbol.Ask + minDist, _symbol);
                if (newSL < minAllowedSL)
                {
                    newSL = minAllowedSL;
                }

                if (position.StopLoss == null || newSL < position.StopLoss)
                {
                    _logger?.Info($"TrailingManager: Triggering Break-even for SELL #{position.Id}. New SL: {newSL}");
                    var result = _robot.ModifyPosition(position, newSL, position.TakeProfit, ProtectionType.Absolute);
                    if (result.IsSuccessful)
                    {
                        track.BreakEvenDone = true;
                    }
                    else
                    {
                        LogModifyFail(position.Id, "Break-even SELL", result.Error);
                    }
                }
            }
        }

        private void LogModifyFail(int positionId, string action, object error)
        {
            string err = error?.ToString() ?? "";
            // Position already closed (TP/SL) — expected race, not an error
            if (err.IndexOf("EntityNotFound", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger?.Debug($"TrailingManager: {action} #{positionId} skipped (position gone)");
                return;
            }
            _logger?.Error($"TrailingManager: {action} failed for #{positionId}. Error: {error}");
        }

        private void ManageTrailing(Position position, PositionTrack track)
        {
            double profitPips = position.Pips;
            // Per-position gate (post-TP1) or global start from entry
            double needPips = track.ActivateAtPips > 0 ? track.ActivateAtPips : _trailStartPips;
            if (profitPips < needPips) return;

            double? newSL = null;
            double trailDistance = PriceUtils.PipsToPrice(_trailStepPips, _symbol);
            double sensitivityDistance = PriceUtils.PipsToPrice(_trailSensitivityPips, _symbol);

            // Calculate minimum allowed distance for Stop Loss
            double minDist = GetMinStopDistancePrice();

            if (position.TradeType == TradeType.Buy)
            {
                newSL = PriceUtils.NormalizePrice(_symbol.Bid - trailDistance, _symbol);

                // Clamp by MinStopLossDistance to avoid invalid SL errors
                double maxAllowedSL = PriceUtils.NormalizePrice(_symbol.Bid - minDist, _symbol);
                if (newSL > maxAllowedSL)
                {
                    newSL = maxAllowedSL;
                }

                if (position.StopLoss == null || newSL >= position.StopLoss + sensitivityDistance)
                {
                    // Ensure we only move SL upwards
                    if (position.StopLoss == null || newSL > position.StopLoss)
                    {
                        _logger?.Debug($"TrailingManager: Trailing BUY #{position.Id}. New SL: {newSL}");
                        var result = _robot.ModifyPosition(position, newSL, position.TakeProfit, ProtectionType.Absolute);
                        if (result.IsSuccessful)
                        {
                            track.TrailingStarted = true;
                        }
                        else
                        {
                            LogModifyFail(position.Id, "Trailing BUY", result.Error);
                        }
                    }
                }
            }
            else if (position.TradeType == TradeType.Sell)
            {
                newSL = PriceUtils.NormalizePrice(_symbol.Ask + trailDistance, _symbol);

                // Clamp by MinStopLossDistance to avoid invalid SL errors
                double minAllowedSL = PriceUtils.NormalizePrice(_symbol.Ask + minDist, _symbol);
                if (newSL < minAllowedSL)
                {
                    newSL = minAllowedSL;
                }

                if (position.StopLoss == null || newSL <= position.StopLoss - sensitivityDistance)
                {
                    // Ensure we only move SL downwards
                    if (position.StopLoss == null || newSL < position.StopLoss)
                    {
                        _logger?.Debug($"TrailingManager: Trailing SELL #{position.Id}. New SL: {newSL}");
                        var result = _robot.ModifyPosition(position, newSL, position.TakeProfit, ProtectionType.Absolute);
                        if (result.IsSuccessful)
                        {
                            track.TrailingStarted = true;
                        }
                        else
                        {
                            LogModifyFail(position.Id, "Trailing SELL", result.Error);
                        }
                    }
                }
            }
        }

        public bool IsAnyTrailingStarted()
        {
            if (_robot == null || _symbol == null) return false;
            var activePositions = _robot.Positions.FindAll(_label, _symbol.Name);
            return _trackedPositions.Any(t => t.TrailingStarted && activePositions.Any(p => p.Id == t.Id));
        }
    }
}
