namespace ResumeParser.Classes
{
    public class CategoryRange
    {
        public int IndexBegin { get; set; }
        public int IndexEnd { get; set; }

        public CategoryRange() 
        {
            IndexBegin = -1;
            IndexEnd = -1;
        }
    }
}
