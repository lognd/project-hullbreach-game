namespace Hullbreach.Bots
{
    // How well a bot plays. Every level is bounded by what a person could do with the same ship:
    // reaction time and decision rate are never below a human's, and aim error is never zero.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#botdifficulty
    public enum BotDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    // Temperament: picks engagement range, how readily the bot retreats, and how loosely it fires.
    // frob:doc docs/reference/hullbreach-bots.md#botpersonality
    public enum BotPersonality
    {
        Balanced = 0,
        Aggressive = 1,
        Cautious = 2,
        Skirmisher = 3,
    }

    // The numbers behind a difficulty/personality pair. Immutable; build one with For.
    // frob:doc docs/reference/hullbreach-bots.md#botprofile
    public sealed class BotProfile
    {
        public BotDifficulty Difficulty { get; private set; }
        public BotPersonality Personality { get; private set; }

        // Ticks (at 50 Hz) between something happening to the enemy and the bot seeing it.
        public int ReactionTicks { get; private set; }

        // Ticks between high-level decisions (state, aim solution); low-level steering runs every tick.
        public int DecisionTicks { get; private set; }

        // Standard deviation of the aim error, in radians, redrawn at every decision.
        public float AimSigma { get; private set; }

        // 0 = aim at where the target is, 1 = full lead on the predicted intercept.
        public float LeadQuality { get; private set; }

        // Whether the lead integrates gravity (orbit-aware) or assumes straight lines.
        public bool GravityAwareLead { get; private set; }

        // Probability that an otherwise good shot is actually taken; below 1 models hesitation.
        public float TriggerDiscipline { get; private set; }

        // Multiplier on the angular tolerance within which the bot will fire.
        public float FireToleranceScale { get; private set; }

        // 0..1: how often a detected incoming shot triggers an evasive burn.
        public float DodgeSkill { get; private set; }

        // Seconds of coasting prediction used to avoid planets.
        public float PlanetLookahead { get; private set; }

        // Preferred distance to the enemy.
        public float EngageRange { get; private set; }

        // Own core damage fraction at which the bot starts to withdraw.
        public float RetreatCoreDamage { get; private set; }

        // Seconds between strafing bursts; 0 disables strafing.
        public float StrafePeriod { get; private set; }

        // Extra range-keeping slack as a fraction of EngageRange.
        public float RangeSlack { get; private set; }

        public static BotProfile For(BotDifficulty difficulty, BotPersonality personality)
        {
            var p = new BotProfile { Difficulty = difficulty, Personality = personality };
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    p.ReactionTicks = 30; p.DecisionTicks = 18; p.AimSigma = 0.075f; p.LeadQuality = 0.35f;
                    p.GravityAwareLead = false; p.TriggerDiscipline = 0.55f; p.FireToleranceScale = 2.2f;
                    p.DodgeSkill = 0f; p.PlanetLookahead = 3f; p.StrafePeriod = 0f; p.RangeSlack = 0.5f;
                    break;
                case BotDifficulty.Normal:
                    p.ReactionTicks = 14; p.DecisionTicks = 9; p.AimSigma = 0.03f; p.LeadQuality = 0.85f;
                    p.GravityAwareLead = true; p.TriggerDiscipline = 0.85f; p.FireToleranceScale = 1.6f;
                    p.DodgeSkill = 0.2f; p.PlanetLookahead = 4f; p.StrafePeriod = 0f; p.RangeSlack = 0.3f;
                    break;
                default:
                    p.ReactionTicks = 9; p.DecisionTicks = 9; p.AimSigma = 0.012f; p.LeadQuality = 1f;
                    p.GravityAwareLead = true; p.TriggerDiscipline = 0.95f; p.FireToleranceScale = 2.0f;
                    p.DodgeSkill = 0.3f; p.PlanetLookahead = 4f; p.StrafePeriod = 0f; p.RangeSlack = 0.3f;
                    break;
            }
            switch (personality)
            {
                case BotPersonality.Aggressive:
                    p.EngageRange = 14f; p.RetreatCoreDamage = 0.85f; p.FireToleranceScale *= 1.25f;
                    break;
                case BotPersonality.Cautious:
                    p.EngageRange = 32f; p.RetreatCoreDamage = 0.35f; p.FireToleranceScale *= 0.85f;
                    break;
                case BotPersonality.Skirmisher:
                    p.EngageRange = 22f; p.RetreatCoreDamage = 0.55f;
                    if (p.StrafePeriod > 0f) p.StrafePeriod *= 0.6f;
                    break;
                default:
                    p.EngageRange = 22f; p.RetreatCoreDamage = 0.6f;
                    break;
            }
            return p;
        }
    }
}
