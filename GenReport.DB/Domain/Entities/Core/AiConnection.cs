using CoreDdd.Domain;
using GenReport.DB.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GenReport.DB.Domain.Entities.Core
{
    /// <summary>
    /// Represents a configured AI/LLM provider connection.
    /// The API key is stored encrypted (AES-256-GCM via CredentialEncryptorFactory).
    /// </summary>
    [Table("ai_connections")]
    public class AiConnection : BaseEntity
    {
        /// <summary>Provider name (e.g. OpenAI, Anthropic, Gemini, Ollama).</summary>
        [Column("provider")]
        [Required]
        [StringLength(100)]
        public required string Provider { get; set; }

        /// <summary>AES-256-GCM encrypted API key. Never returned in API responses.</summary>
        [Column("api_key")]
        [Required]
        public required string ApiKey { get; set; }

        /// <summary>Default model identifier (e.g. gpt-4o, claude-3-5-sonnet-20241022).</summary>
        [Column("default_model")]
        [Required]
        [StringLength(100)]
        public required string DefaultModel { get; set; }

        /// <summary>Optional system-level prompt injected into every request.</summary>
        [Column("system_prompt")]
        public string? SystemPrompt { get; set; }

        /// <summary>Sampling temperature (0.0–2.0). Null means provider default.</summary>
        [Column("temperature")]
        public double? Temperature { get; set; }

        /// <summary>Maximum tokens per completion. Null means provider default.</summary>
        [Column("max_tokens")]
        public int? MaxTokens { get; set; }

        /// <summary>Rate limit: maximum requests per minute (RPM).</summary>
        [Column("rate_limit_rpm")]
        public int? RateLimitRpm { get; set; }

        /// <summary>Rate limit: maximum tokens per minute (TPM).</summary>
        [Column("rate_limit_tpm")]
        public int? RateLimitTpm { get; set; }

        /// <summary>Cost per 1,000 input (prompt) tokens in USD, for cost-tracking reports.</summary>
        [Column("cost_per_1k_input_tokens")]
        public decimal? CostPer1kInputTokens { get; set; }

        /// <summary>Cost per 1,000 output (completion) tokens in USD.</summary>
        [Column("cost_per_1k_output_tokens")]
        public decimal? CostPer1kOutputTokens { get; set; }

        /// <summary>Whether this connection is active and usable.</summary>
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Whether this is the default connection for this provider.
        /// Only one connection per provider should have IsDefault = true.
        /// </summary>
        [Column("is_default")]
        public bool IsDefault { get; set; } = false;

    }
}
