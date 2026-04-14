namespace Recrutement_api.Services.AI
{
    /// <summary>
    /// Le service Python (FastAPI) ou Groq a renvoyé une erreur HTTP ; le corps est conservé pour le client.
    /// </summary>
    public sealed class AiUpstreamException : Exception
    {
        public int HttpStatus { get; }

        /// <summary>Corps brut (souvent JSON FastAPI avec <c>detail</c>).</summary>
        public string ResponseBody { get; }

        public AiUpstreamException(int httpStatus, string responseBody, string message)
            : base(message)
        {
            HttpStatus = httpStatus;
            ResponseBody = responseBody ?? string.Empty;
        }
    }
}
