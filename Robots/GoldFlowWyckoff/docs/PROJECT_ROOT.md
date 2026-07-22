# Gold Flow Wyckoff Confluence cBot

Root tracker cho cBot **Gold Flow Wyckoff Confluence v1.0** trên cTrader.

## Project Identity
- **Name:** GoldFlowWyckoff
- **Platform:** cTrader (C#)
- **Symbol:** XAUUSD
- **Primary Timeframes:** Execution M5 / Signal M15, Trend H1-H4
- **Version:** v1.0

## Directory Structure
- `docs/1-prds/PRD-gold-flow-wyckoff.md`: Product Requirements Document
- `docs/3-plans/PLAN-gold-flow-wyckoff.md`: Task breakdown and implementation plan
- `GoldFlowWyckoff/GoldFlowWyckoff.cs`: Main cBot entry point & lifecycle
- `GoldFlowWyckoff/SignalEngine.cs`: Pure, decoupled signal evaluation engine (`SignalContext` & `SignalResult`)
- `../../Common/WyckoffWaveEngine.cs`: Shared Weis & Wyckoff wave & pivot calculation engine (RedWave.Common)
- `GoldFlowWyckoff/GoldFlowWyckoff.csproj`: C# Project referencing `../../Common/*.cs`

## Status
- **PRD:** In Progress / Draft
- **Plan:** In Progress / Draft
- **Code:** Pending PRD approval
