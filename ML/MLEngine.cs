using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;

namespace ResumeParser.ML
{
    public class MLEngine
    {
        private string _trainDataPath;
        private string _modelPath;
        private MLContext _mlContext;
        private PredictionEngine<TextArea, AreaPrediction> _predEngine;
        private ITransformer _trainedModel;
        private IDataView _trainingDataView;
        private bool showMessages;

        public MLEngine(string trainDataPath, bool shoMsg) 
        {
            _trainDataPath = trainDataPath;
            _modelPath = $"{Path.GetDirectoryName(trainDataPath)}\\model.zip" ;
            showMessages = shoMsg;
            // Create MLContext to be shared across the model creation workflow objects
            // Set a random seed for repeatable/deterministic results across multiple trainings.
            _mlContext = new MLContext(seed: 0);
        }

        public void PrepareToTrain()
        {
            if (showMessages) Console.WriteLine("Loading Model Dataset");
            // Common data loading configuration
            _trainingDataView = _mlContext.Data.LoadFromTextFile<TextArea>(_trainDataPath, hasHeader: true);
        }

        public void TrainModel()
        {
            var pipeline = ProcessData();
            var trainingPipeline = BuildAndTrainModel(_trainingDataView, pipeline);
            SaveModelAsFile(_mlContext, _trainingDataView.Schema, _trainedModel);
        }

        public void PrepareToPredict(bool isSinglePrediction)
        {
            _trainedModel = _mlContext.Model.Load(_modelPath, out var modelInputSchema);
            if (isSinglePrediction)
            {
                _predEngine = _mlContext.Model.CreatePredictionEngine<TextArea, AreaPrediction>(_trainedModel);
            }
        }

        public AreaPrediction SinglePredict(TextArea inputData)
        {
            return _predEngine.Predict(inputData);
        }

        public IEnumerable<AreaPrediction> BatchPredict(IEnumerable<TextArea> inputData)
        {
            IDataView batchTextArea = _mlContext.Data.LoadFromEnumerable(inputData);
            IDataView predictions = _trainedModel.Transform(batchTextArea);
            return _mlContext.Data.CreateEnumerable<AreaPrediction>(predictions, reuseRowObject: false);
        }

        public IEstimator<ITransformer> ProcessData()
        {
            if (showMessages) Console.WriteLine("Processing Data");
            // Common data process configuration with pipeline data transformations
            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(inputColumnName: "Area", outputColumnName: "Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText(inputColumnName: "Title", outputColumnName: "TitleFeaturized"))
                .Append(_mlContext.Transforms.Text.FeaturizeText(inputColumnName: "Description", outputColumnName: "DescriptionFeaturized"))
                .Append(_mlContext.Transforms.Concatenate("Features", "TitleFeaturized", "DescriptionFeaturized"))
                //Sample Caching the DataView so estimators iterating over the data multiple times, instead of always reading from file, using the cache might get better performance.
                .AppendCacheCheckpoint(_mlContext);
            if (showMessages) Console.WriteLine($"Finished Processing Data");
            return pipeline;
        }

        public IEstimator<ITransformer> BuildAndTrainModel(IDataView trainingDataView , IEstimator<ITransformer> pipeline)
        {
            // Create the training algorithm/trainer
            // Use the multi-class SDCA algorithm to predict the label using features.
            // Set the trainer/algorithm and map label to value (original readable state)
            var trainingPipeline = pipeline.Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // Train the model fitting to the DataSet
            if (showMessages) Console.WriteLine($"Training the model, Starting time: {DateTime.Now.ToString()}");

            _trainedModel = trainingPipeline.Fit(trainingDataView);

            if (showMessages) Console.WriteLine($"Finished Training the model, Ending time: {DateTime.Now.ToString()}");

            return trainingPipeline;
        }

        private void SaveModelAsFile(MLContext mlContext, DataViewSchema trainingDataViewSchema, ITransformer model)
        {
            mlContext.Model.Save(model, trainingDataViewSchema, _modelPath);
            if (showMessages) Console.WriteLine("The model is saved to {0}", _modelPath);
        }
    }
}
