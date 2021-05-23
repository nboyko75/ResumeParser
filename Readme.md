# Resume parser
Resume parser extracts text from .docx and .pdf resume files and parse data to output json file.

# Directory structure
Parser folders:
- Doc - folder for .docx input resume files:
- Pdf - folder for .pdf input resume files;
- Json - folder for output json files;
- Model - folder for dictionary data for parsing.

# Model setup
The quality of parsing depends on correct model setup. For this purpose folder <Model> contains files:
 - <Category>.<Attribute>.txt - dictionaries for recognition of <Category>.<Attribute>;
 - Marker.json - markers for recognition category position in a text;
 - Data.txt - data for machine learning training (need for gathering data).

 # Command line
	Train mode usage: ResumeParser -mode train -data TRAINDATAPATH
	Parse mode usage: ResumeParser -mode parse -data TRAINDATAPATH -pdf PDFPATH -doc DOCPATH -json JSONPATH
- mode - 'train' or 'parse'
- data - the absolute path to the train data path, reguired
- pdf - the absolute path to PDF files folder, optional
- doc - the absolute path to MS WORD files folder, optional
- json - the absolute path to JSON files folder, reguired

# Get started
1. Copy resume files into folders <Doc> and <Pdf>.
2. Edit runTrain.cmd and runParse.cmd files for correct folder paths.
3. Run runTrain.cmd once after <Model>\Data.txt has been changed.
4. Run runParse.cmd for parsing after <Doc>, <Pdf> or <Model> folder has been changed.
5. Have a look on json files in <Json> folder.