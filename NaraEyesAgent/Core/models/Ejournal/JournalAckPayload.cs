

namespace NaraEyesAgent.Core.Models.Ejournal
{
    public sealed class JournalAckPayload
    {
        public Guid CommandId { get; set; }
        public string DataBase64 { get; set; }      
        public string ContentType { get; set; }    
        public string FileName { get; set; }        
        public string Message { get; set; }         
    }
}
