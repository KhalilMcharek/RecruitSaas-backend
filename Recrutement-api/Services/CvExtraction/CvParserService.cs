using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Recrutement_api.Services.CvExtraction.Extractors;

namespace Recrutement_api.Services.CvExtraction
{
    public class CvParserService : ICvParserService
    {
        private readonly PdfTextExtractor _pdfExtractor;
        private readonly DocxTextExtractor _docxExtractor;
        private readonly OcrExtractor _ocrExtractor;
        private readonly HttpClient _httpClient;

        public CvParserService(PdfTextExtractor pdfExtractor, DocxTextExtractor docxExtractor, OcrExtractor ocrExtractor, HttpClient httpClient)
        {
            _pdfExtractor = pdfExtractor;
            _docxExtractor = docxExtractor;
            _ocrExtractor = ocrExtractor;
            _httpClient = httpClient;
        }

        public async Task<string> ExtractTextAsync(string fileUrl)
        {
            string tempFilePath = Path.GetTempFileName();
            string textResult = "";

            try
            {
                byte[] fileBytes = await _httpClient.GetByteArrayAsync(fileUrl);
                
                string ext = ".pdf";
                if (fileUrl.ToLower().Contains(".doc") || fileUrl.ToLower().Contains(".docx"))
                {
                    ext = ".docx";
                }
                else if (fileUrl.ToLower().Contains(".png") || fileUrl.ToLower().Contains(".jpg") || fileUrl.ToLower().Contains(".jpeg"))
                {
                    ext = ".jpg";
                }
                
                tempFilePath = tempFilePath + ext;
                await File.WriteAllBytesAsync(tempFilePath, fileBytes);

                if (ext == ".pdf")
                {
                    textResult = await _pdfExtractor.ExtractTextFromFileAsync(tempFilePath);
                }
                else if (ext == ".docx")
                {
                    textResult = await _docxExtractor.ExtractTextFromFileAsync(tempFilePath);
                }
                else
                {
                    // OCR
                    textResult = await _ocrExtractor.ExtractTextFromFileAsync(tempFilePath);
                }
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { }
                }
            }

            return textResult ?? "";
        }
    }
}