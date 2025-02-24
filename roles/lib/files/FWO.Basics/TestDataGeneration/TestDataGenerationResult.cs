namespace FWO.Basics.TestDataGeneration
{
    public class TestDataGenerationResult<T>
    {
        public bool ProcessSuccessful { get; set; }
        public T? SingleInstance { get; set; }
        public List<T>? Collection { get; set; }
        public T SubjectUnderTest { get; set; }
    }
}
