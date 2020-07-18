namespace Bot.DiscordRelated.Criteria {
    public static class CriterionUtils {
        public static ICriterion Invert(this ICriterion criterion) {
            return new CustomCriterion(async () => !await criterion.JudgeAsync());
        }
    }
}