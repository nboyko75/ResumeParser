using Microsoft.ML.Data;

namespace ResumeParser.ML
{
    public class TextArea
    {
        [LoadColumn(0)]
        public string Area { get; set; }
        [LoadColumn(1)]
        public string Title { get; set; }
        [LoadColumn(2)]
        public string Description { get; set; }
    }

    public class AreaPrediction /*: TextArea */
    {
        [ColumnName("PredictedLabel")]
        public string PredictedArea;
    }
}
