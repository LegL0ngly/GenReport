namespace GenReport.Infrastructure.Models.Shared
{
    using GenReport.Infrastructure.Static.Constants;
    using System;
    using System.Net;

    /// <summary>
    /// Defines the <see cref="HttpSuccessResponse{T}" />
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class HttpSuccessResponse<T> where T : class
    {
        /// <summary>
        /// Gets or sets a value indicating whether Success
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Gets or sets the StatusCode
        /// </summary>
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;

        /// <summary>
        /// Gets or sets the Message
        /// </summary>
        public string Message { get; set; } = GenericConstants.DEFAULT_SUCCESS_MESSAGE;

        /// <summary>
        /// Gets or sets the Data
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// Gets or sets the Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;


        /// <summary>
        /// Parameterless constructor required by System.Text.Json for deserialization.
        /// </summary>
        public HttpSuccessResponse() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpSuccessResponse{T}"/> class.
        /// </summary>
        /// <param name="data">The data<see cref="T"/></param>
        public HttpSuccessResponse(T data)
        {
            Data = data;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpSuccessResponse{T}"/> class.
        /// </summary>
        /// <param name="data">The data<see cref="T"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        public HttpSuccessResponse(T data, string message)
        {
            Data = data;
            Message = message;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpSuccessResponse{T}"/> class.
        /// </summary>
        /// <param name="data">The data<see cref="T"/></param>
        /// <param name="statusCode">The statusCode<see cref="HttpStatusCode"/></param>
        public HttpSuccessResponse(T data, HttpStatusCode statusCode)
        {
            Data = data;
            StatusCode = statusCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpSuccessResponse{T}"/> class.
        /// </summary>
        /// <param name="data">The data<see cref="T"/></param>
        /// <param name="message">The message<see cref="string"/></param>
        /// <param name="statusCode">The statusCode<see cref="HttpStatusCode"/></param>
        public HttpSuccessResponse(T data, string message, HttpStatusCode statusCode)
        {
            Data = data;
            Message = message;
            StatusCode = statusCode;
        }
    }
}
