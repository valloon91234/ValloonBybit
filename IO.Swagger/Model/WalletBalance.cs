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
    /// Get wallet balance response
    /// </summary>
    [DataContract]
    public partial class WalletBalance :  IEquatable<WalletBalance>, IValidatableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WalletBalance" /> class.
        /// </summary>
        /// <param name="equity">equity.</param>
        /// <param name="availableBalance">availableBalance.</param>
        /// <param name="usedMargin">usedMargin.</param>
        /// <param name="orderMargin">orderMargin.</param>
        /// <param name="positionMargin">positionMargin.</param>
        /// <param name="occClosingFee">occClosingFee.</param>
        /// <param name="occFundingFee">occFundingFee.</param>
        /// <param name="walletBalance">walletBalance.</param>
        /// <param name="realisedPnl">realisedPnl.</param>
        /// <param name="unrealisedPnl">unrealisedPnl.</param>
        /// <param name="cumRealisedPnl">cumRealisedPnl.</param>
        /// <param name="givenCash">givenCash.</param>
        /// <param name="serviceCash">serviceCash.</param>
        public WalletBalance(decimal? equity = default(decimal?), decimal? availableBalance = default(decimal?), decimal? usedMargin = default(decimal?), decimal? orderMargin = default(decimal?), decimal? positionMargin = default(decimal?), decimal? occClosingFee = default(decimal?), decimal? occFundingFee = default(decimal?), decimal? walletBalance = default(decimal?), decimal? realisedPnl = default(decimal?), decimal? unrealisedPnl = default(decimal?), decimal? cumRealisedPnl = default(decimal?), decimal? givenCash = default(decimal?), decimal? serviceCash = default(decimal?))
        {
            this.Equity = equity;
            this.AvailableBalance = availableBalance;
            this.UsedMargin = usedMargin;
            this.OrderMargin = orderMargin;
            this.PositionMargin = positionMargin;
            this.OccClosingFee = occClosingFee;
            this.OccFundingFee = occFundingFee;
            this._WalletBalance = walletBalance;
            this.RealisedPnl = realisedPnl;
            this.UnrealisedPnl = unrealisedPnl;
            this.CumRealisedPnl = cumRealisedPnl;
            this.GivenCash = givenCash;
            this.ServiceCash = serviceCash;
        }
        
        /// <summary>
        /// Gets or Sets Equity
        /// </summary>
        [DataMember(Name="equity", EmitDefaultValue=false)]
        public decimal? Equity { get; set; }

        /// <summary>
        /// Gets or Sets AvailableBalance
        /// </summary>
        [DataMember(Name="available_balance", EmitDefaultValue=false)]
        public decimal? AvailableBalance { get; set; }

        /// <summary>
        /// Gets or Sets UsedMargin
        /// </summary>
        [DataMember(Name="used_margin", EmitDefaultValue=false)]
        public decimal? UsedMargin { get; set; }

        /// <summary>
        /// Gets or Sets OrderMargin
        /// </summary>
        [DataMember(Name="order_margin", EmitDefaultValue=false)]
        public decimal? OrderMargin { get; set; }

        /// <summary>
        /// Gets or Sets PositionMargin
        /// </summary>
        [DataMember(Name="position_margin", EmitDefaultValue=false)]
        public decimal? PositionMargin { get; set; }

        /// <summary>
        /// Gets or Sets OccClosingFee
        /// </summary>
        [DataMember(Name="occ_closing_fee", EmitDefaultValue=false)]
        public decimal? OccClosingFee { get; set; }

        /// <summary>
        /// Gets or Sets OccFundingFee
        /// </summary>
        [DataMember(Name="occ_funding_fee", EmitDefaultValue=false)]
        public decimal? OccFundingFee { get; set; }

        /// <summary>
        /// Gets or Sets _WalletBalance
        /// </summary>
        [DataMember(Name="wallet_balance", EmitDefaultValue=false)]
        public decimal? _WalletBalance { get; set; }

        /// <summary>
        /// Gets or Sets RealisedPnl
        /// </summary>
        [DataMember(Name="realised_pnl", EmitDefaultValue=false)]
        public decimal? RealisedPnl { get; set; }

        /// <summary>
        /// Gets or Sets UnrealisedPnl
        /// </summary>
        [DataMember(Name="unrealised_pnl", EmitDefaultValue=false)]
        public decimal? UnrealisedPnl { get; set; }

        /// <summary>
        /// Gets or Sets CumRealisedPnl
        /// </summary>
        [DataMember(Name="cum_realised_pnl", EmitDefaultValue=false)]
        public decimal? CumRealisedPnl { get; set; }

        /// <summary>
        /// Gets or Sets GivenCash
        /// </summary>
        [DataMember(Name="given_cash", EmitDefaultValue=false)]
        public decimal? GivenCash { get; set; }

        /// <summary>
        /// Gets or Sets ServiceCash
        /// </summary>
        [DataMember(Name="service_cash", EmitDefaultValue=false)]
        public decimal? ServiceCash { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class WalletBalance {\n");
            sb.Append("  Equity: ").Append(Equity).Append("\n");
            sb.Append("  AvailableBalance: ").Append(AvailableBalance).Append("\n");
            sb.Append("  UsedMargin: ").Append(UsedMargin).Append("\n");
            sb.Append("  OrderMargin: ").Append(OrderMargin).Append("\n");
            sb.Append("  PositionMargin: ").Append(PositionMargin).Append("\n");
            sb.Append("  OccClosingFee: ").Append(OccClosingFee).Append("\n");
            sb.Append("  OccFundingFee: ").Append(OccFundingFee).Append("\n");
            sb.Append("  _WalletBalance: ").Append(_WalletBalance).Append("\n");
            sb.Append("  RealisedPnl: ").Append(RealisedPnl).Append("\n");
            sb.Append("  UnrealisedPnl: ").Append(UnrealisedPnl).Append("\n");
            sb.Append("  CumRealisedPnl: ").Append(CumRealisedPnl).Append("\n");
            sb.Append("  GivenCash: ").Append(GivenCash).Append("\n");
            sb.Append("  ServiceCash: ").Append(ServiceCash).Append("\n");
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
            return this.Equals(input as WalletBalance);
        }

        /// <summary>
        /// Returns true if WalletBalance instances are equal
        /// </summary>
        /// <param name="input">Instance of WalletBalance to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(WalletBalance input)
        {
            if (input == null)
                return false;

            return 
                (
                    this.Equity == input.Equity ||
                    (this.Equity != null &&
                    this.Equity.Equals(input.Equity))
                ) && 
                (
                    this.AvailableBalance == input.AvailableBalance ||
                    (this.AvailableBalance != null &&
                    this.AvailableBalance.Equals(input.AvailableBalance))
                ) && 
                (
                    this.UsedMargin == input.UsedMargin ||
                    (this.UsedMargin != null &&
                    this.UsedMargin.Equals(input.UsedMargin))
                ) && 
                (
                    this.OrderMargin == input.OrderMargin ||
                    (this.OrderMargin != null &&
                    this.OrderMargin.Equals(input.OrderMargin))
                ) && 
                (
                    this.PositionMargin == input.PositionMargin ||
                    (this.PositionMargin != null &&
                    this.PositionMargin.Equals(input.PositionMargin))
                ) && 
                (
                    this.OccClosingFee == input.OccClosingFee ||
                    (this.OccClosingFee != null &&
                    this.OccClosingFee.Equals(input.OccClosingFee))
                ) && 
                (
                    this.OccFundingFee == input.OccFundingFee ||
                    (this.OccFundingFee != null &&
                    this.OccFundingFee.Equals(input.OccFundingFee))
                ) && 
                (
                    this._WalletBalance == input._WalletBalance ||
                    (this._WalletBalance != null &&
                    this._WalletBalance.Equals(input._WalletBalance))
                ) && 
                (
                    this.RealisedPnl == input.RealisedPnl ||
                    (this.RealisedPnl != null &&
                    this.RealisedPnl.Equals(input.RealisedPnl))
                ) && 
                (
                    this.UnrealisedPnl == input.UnrealisedPnl ||
                    (this.UnrealisedPnl != null &&
                    this.UnrealisedPnl.Equals(input.UnrealisedPnl))
                ) && 
                (
                    this.CumRealisedPnl == input.CumRealisedPnl ||
                    (this.CumRealisedPnl != null &&
                    this.CumRealisedPnl.Equals(input.CumRealisedPnl))
                ) && 
                (
                    this.GivenCash == input.GivenCash ||
                    (this.GivenCash != null &&
                    this.GivenCash.Equals(input.GivenCash))
                ) && 
                (
                    this.ServiceCash == input.ServiceCash ||
                    (this.ServiceCash != null &&
                    this.ServiceCash.Equals(input.ServiceCash))
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
                if (this.Equity != null)
                    hashCode = hashCode * 59 + this.Equity.GetHashCode();
                if (this.AvailableBalance != null)
                    hashCode = hashCode * 59 + this.AvailableBalance.GetHashCode();
                if (this.UsedMargin != null)
                    hashCode = hashCode * 59 + this.UsedMargin.GetHashCode();
                if (this.OrderMargin != null)
                    hashCode = hashCode * 59 + this.OrderMargin.GetHashCode();
                if (this.PositionMargin != null)
                    hashCode = hashCode * 59 + this.PositionMargin.GetHashCode();
                if (this.OccClosingFee != null)
                    hashCode = hashCode * 59 + this.OccClosingFee.GetHashCode();
                if (this.OccFundingFee != null)
                    hashCode = hashCode * 59 + this.OccFundingFee.GetHashCode();
                if (this._WalletBalance != null)
                    hashCode = hashCode * 59 + this._WalletBalance.GetHashCode();
                if (this.RealisedPnl != null)
                    hashCode = hashCode * 59 + this.RealisedPnl.GetHashCode();
                if (this.UnrealisedPnl != null)
                    hashCode = hashCode * 59 + this.UnrealisedPnl.GetHashCode();
                if (this.CumRealisedPnl != null)
                    hashCode = hashCode * 59 + this.CumRealisedPnl.GetHashCode();
                if (this.GivenCash != null)
                    hashCode = hashCode * 59 + this.GivenCash.GetHashCode();
                if (this.ServiceCash != null)
                    hashCode = hashCode * 59 + this.ServiceCash.GetHashCode();
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
