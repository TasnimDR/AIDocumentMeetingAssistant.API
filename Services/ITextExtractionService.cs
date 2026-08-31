using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AIDocumentMeetingAssistant.API.Services
{
    public interface ITextExtractionService
    {
        Task<string> ExtractTextAsync(IFormFile file);
        string GetFileExtension(string fileName);
    }
}