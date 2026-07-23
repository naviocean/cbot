namespace RedWave.Common.Smc
{
    /// <summary>
    /// Type of Market Structure Pivot Point.
    /// </summary>
    public enum StructureType
    {
        SwingHigh,
        SwingLow
    }

    /// <summary>
    /// Type of Market Structure Break Event.
    /// </summary>
    public enum BreakType
    {
        BOS,   // Break of Structure (Continuation)
        ChoCH, // Change of Character (Reversal)
        MSS    // Market Structure Shift (ICT Impulse Reversal with FVG)
    }

    /// <summary>
    /// Lifecycle state of a Fair Value Gap (FVG).
    /// </summary>
    public enum FvgStatus
    {
        Active,          // Unfilled gap
        PartiallyFilled, // Touched 50% CE
        Mitigated,       // Filled gap according to MitigationMode
        Invalidated      // Broken beyond origin
    }

    /// <summary>
    /// Condition threshold to consider an FVG as Mitigated (Filled).
    /// </summary>
    public enum FvgMitigationMode
    {
        TouchEdge,   // Price touches entry edge of FVG (TopPrice for Buy, BottomPrice for Sell)
        HalfFillCE,  // Price reaches 50% Consequent Encroachment (CE)
        FullFill     // Price fully covers the opposite edge of FVG (BottomPrice for Buy, TopPrice for Sell)
    }

    /// <summary>
    /// Type of Order Block / Supply & Demand zone.
    /// </summary>
    public enum ObType
    {
        BullishOB,
        BearishOB,
        BreakerBlock,
        MitigationBlock
    }

    /// <summary>
    /// Type of Liquidity Pool / Level.
    /// </summary>
    public enum LiquidityType
    {
        BSL,       // Buy Side Liquidity
        SSL,       // Sell Side Liquidity
        EQH,       // Equal Highs
        EQL,       // Equal Lows
        AsianHigh, // Asian Session High
        AsianLow,  // Asian Session Low
        PDH,       // Previous Day High
        PDL        // Previous Day Low
    }

    /// <summary>
    /// Dealing Range Price Zone (Fibonacci 50% Equilibrium).
    /// </summary>
    public enum MarketZone
    {
        Premium,    // Above 50% Equilibrium (Sell Zone)
        Discount,   // Below 50% Equilibrium (Buy Zone)
        Equilibrium // 50% Equilibrium Line
    }
}
