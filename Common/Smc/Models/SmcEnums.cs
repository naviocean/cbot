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
        Invalidated,     // Broken beyond origin
        Inversion        // Inverted role (Inversion FVG iFVG)
    }

    /// <summary>
    /// Condition threshold to consider an FVG as Mitigated (Filled).
    /// </summary>
    public enum FvgMitigationMode
    {
        TouchEdge,   // Price touches entry edge of FVG
        HalfFillCE,  // Price reaches 50% Consequent Encroachment (CE)
        FullFill     // Price fully covers the opposite edge of FVG
    }

    /// <summary>
    /// Type of ICT Open Gap (NWOG / NDOG).
    /// </summary>
    public enum OpenGapType
    {
        NWOG, // New Week Open Gap
        NDOG  // New Day Open Gap
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
        PDL,       // Previous Day Low
        PWH,       // Previous Week High
        PWL        // Previous Week Low
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

    /// <summary>
    /// Trading Session Type (ICT Sessions).
    /// </summary>
    public enum SessionType
    {
        Asian,
        London,
        NewYork,
        OffSession
    }

    /// <summary>
    /// ICT High-Probability Trading Windows (Kill Zones).
    /// </summary>
    public enum KillZone
    {
        None,
        LOKZ,          // London Open Kill Zone (02:00 - 05:00 UTC)
        NYAM,          // NY AM Kill Zone (07:00 - 10:00 UTC)
        NYPM,          // NY PM Kill Zone (13:30 - 16:00 UTC)
        SilverBullet1, // SB1 (10:00 - 11:00 UTC)
        SilverBullet2, // SB2 (14:00 - 15:00 UTC)
        SilverBullet3  // SB3 (15:00 - 16:00 UTC)
    }

    /// <summary>
    /// ICT Power of Three (PO3) Phase.
    /// </summary>
    public enum Po3Phase
    {
        None,
        Accumulation,
        Manipulation,
        Distribution
    }

    /// <summary>
    /// Daily Directional Bias.
    /// </summary>
    public enum BiasType
    {
        BuyBias,
        SellBias,
        Neutral
    }
}
