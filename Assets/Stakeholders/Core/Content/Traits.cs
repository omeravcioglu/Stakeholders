namespace Stakeholders.Core
{
    /// <summary>Numeric personality profile used by AI partners.</summary>
    public struct Traits
    {
        /// <summary>Weight on personal money.</summary>
        public double Greed;
        /// <summary>Weight on the company's success.</summary>
        public double Loyalty;
        /// <summary>Appetite for risk.</summary>
        public double Risk;
        /// <summary>How many actions per day they take.</summary>
        public double Diligence;
        /// <summary>Refuses bribes, keeps promises.</summary>
        public double Honesty;
        /// <summary>Wants ownership and authority.</summary>
        public double Ambition;
        /// <summary>Preferred skill trees, in order.</summary>
        public string[] Trees;

        public static Traits For(Personality p)
        {
            switch (p)
            {
                case Personality.Greedy:
                    return new Traits { Greed = 0.9, Loyalty = 0.35, Risk = 0.5, Diligence = 0.5, Honesty = 0.25, Ambition = 0.5, Trees = new[] { "Intrigue", "Finance", "Negotiation" } };
                case Personality.Loyal:
                    return new Traits { Greed = 0.2, Loyalty = 0.95, Risk = 0.4, Diligence = 0.65, Honesty = 0.95, Ambition = 0.3, Trees = new[] { "Management", "Negotiation", "Finance", "Intrigue" } };
                case Personality.RiskTaker:
                    return new Traits { Greed = 0.5, Loyalty = 0.6, Risk = 0.95, Diligence = 0.6, Honesty = 0.6, Ambition = 0.6, Trees = new[] { "Marketing", "Product Research", "Finance" } };
                case Personality.Conservative:
                    return new Traits { Greed = 0.4, Loyalty = 0.75, Risk = 0.1, Diligence = 0.5, Honesty = 0.8, Ambition = 0.3, Trees = new[] { "Finance", "Negotiation", "Management" } };
                case Personality.Workaholic:
                    return new Traits { Greed = 0.4, Loyalty = 0.8, Risk = 0.5, Diligence = 1.0, Honesty = 0.7, Ambition = 0.6, Trees = new[] { "Product Research", "Management", "Marketing", "Negotiation" } };
                case Personality.Lazy:
                    return new Traits { Greed = 0.6, Loyalty = 0.5, Risk = 0.4, Diligence = 0.15, Honesty = 0.6, Ambition = 0.2, Trees = new[] { "Finance", "Marketing" } };
                case Personality.Manipulative:
                    return new Traits { Greed = 0.7, Loyalty = 0.4, Risk = 0.6, Diligence = 0.6, Honesty = 0.1, Ambition = 0.8, Trees = new[] { "Intrigue", "Management", "Negotiation" } };
                case Personality.Ambitious:
                    return new Traits { Greed = 0.6, Loyalty = 0.6, Risk = 0.6, Diligence = 0.8, Honesty = 0.45, Ambition = 1.0, Trees = new[] { "Management", "Marketing", "Product Research", "Intrigue" } };
            }
            return new Traits { Greed = 0.5, Loyalty = 0.7, Risk = 0.5, Diligence = 0.6, Honesty = 0.7, Ambition = 0.5, Trees = new[] { "Product Research", "Negotiation", "Marketing" } };
        }
    }
}
