namespace GenReport.Infrastructure.Models.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;

    /// <summary>
    /// Represents an HTTP response that can be either a success or an error.
    /// Only one of HttpSuccessResponse or HttpErrorResponse will be non-null at any given time.
    /// </summary>
    /// <typeparam name="T">The type of the data returned in case of a successful response.</typeparam>
    public class HttpResponse<T> where T : class
    {
        /// <summary>
        /// Gets or sets the success response.
        /// This will be populated if the response is successful.
        /// </summary>
        public HttpSuccessResponse<T>? SuccessResponse { get; set; }

        /// <summary>
        /// Gets or sets the error response.
        /// This will be populated if the response is an error.
        /// </summary>
        public HttpErrorResponse? ErrorResponse { get; set; }

        /// <summary>
        /// Parameterless constructor required by System.Text.Json for deserialization.
        /// </summary>
        public HttpResponse() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponse{T}"/> class for a successful response.
        /// Only the SuccessResponse property will be populated.
        /// </summary>
        /// <param name="data">The data returned in the success response.</param>
        /// <param name="message">The success message.</param>
        /// <param name="statusCode">The HTTP status code for the success response.</param>
        public HttpResponse(T data, string message = "Operation completed successfully.", HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            SuccessResponse = new HttpSuccessResponse<T>(data, message, statusCode);
            ErrorResponse = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpResponse{T}"/> class for an error response.
        /// Only the ErrorResponse property will be populated.
        /// </summary>
        /// <param name="statusCode">The HTTP status code for the error response.</param>
        /// <param name="message">The error message.</param>
        /// <param name="errorCode">The error code.</param>
        /// <param name="errors">The list of detailed errors.</param>
        public HttpResponse(HttpStatusCode statusCode, string message = "An error occurred.", string errorCode = "ERR001", List<string>? errors = null)
        {
            SuccessResponse = null;
            ErrorResponse = new HttpErrorResponse(statusCode, message, errorCode, errors ?? new List<string>());
        }
    }

}
