namespace GenReport.DB.Domain.Enums
{
    /// <summary>
    /// Represents the functional category of an AI model endpoint.
    /// </summary>
    public enum AiEndpointType
    {
        /// <summary>Chat/completion endpoint (e.g. /v1/chat/completions).</summary>
        Chat = 1,

        /// <summary>Model listing endpoint (e.g. /v1/models).</summary>
        Models = 2,

        /// <summary>Usage/quota endpoint (e.g. /v1/usage).</summary>
        Quota = 3,
    }
}
