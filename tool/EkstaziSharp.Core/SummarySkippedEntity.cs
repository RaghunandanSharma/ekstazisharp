namespace EkstaziSharp.Model
{
    public abstract class SummarySkippedEntity : SkippedEntity
    {
        protected SummarySkippedEntity()
        {
            Summary = new Summary();
        } 

        public Summary Summary { get; set; }

        public bool ShouldSerializeSummary() { return !ShouldSerializeSkippedDueTo(); }
    }
}