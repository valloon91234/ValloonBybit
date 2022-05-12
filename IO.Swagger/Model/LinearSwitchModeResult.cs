/* 
 * Bybit API
 *
 * ## REST API for the Bybit Exchange. Base URI: [https://api.bybit.com]  
 *
 * OpenAPI spec version: 0.2.10
 * Contact: support@bybit.com
 * Generated by: https://github.com/swagger-api/swagger-codegen.git
 */

using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel.DataAnnotations;
using SwaggerDateConverter = IO.Swagger.Client.SwaggerDateConverter;

namespace IO.Swagger.Model
{
    /// <summary>
    /// LinearSwitchModeResult
    /// </summary>
    [DataContract]
    public partial class LinearSwitchModeResult :  IEquatable<LinearSwitchModeResult>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LinearSwitchModeResult" /> class.
        /// </summary>
        /// <param name="tpSlMode">tpSlMode.</param>
        public LinearSwitchModeResult(double? tpSlMode = default(double?))
        {
            this.TpSlMode = tpSlMode;
        }
        
        /// <summary>
        /// Gets or Sets TpSlMode
        /// </summary>
        [DataMember(Name="tp_sl_mode", EmitDefaultValue=false)]
        public double? TpSlMode { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class LinearSwitchModeResult {\n");
            sb.Append("  TpSlMode: ").Append(TpSlMode).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }
  
        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="input">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object input)
        {
            return this.Equals(input as LinearSwitchModeResult);
        }

        /// <summary>
        /// Returns true if LinearSwitchModeResult instances are equal
        /// </summary>
        /// <param name="input">Instance of LinearSwitchModeResult to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(LinearSwitchModeResult input)
        {
            if (input == null)
                return false;

            return 
                (
                    this.TpSlMode == input.TpSlMode ||
                    (this.TpSlMode != null &&
                    this.TpSlMode.Equals(input.TpSlMode))
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hashCode = 41;
                if (this.TpSlMode != null)
                    hashCode = hashCode * 59 + this.TpSlMode.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// To validate all properties of the instance
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation Result</returns>
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            yield break;
        }
    }

}
