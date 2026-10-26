using System.ComponentModel.DataAnnotations;
namespace CeyvoriaTours.Application;
public record ApiResponse<T>(bool Success,T? Data,string? Message=null);
public record ReferenceResponse(string Reference);
public class ContactRequest {
 [Required,MinLength(2),MaxLength(150)]public string Name{get;set;}="";
 [Required,EmailAddress,MaxLength(254)]public string Email{get;set;}="";
 [Required,MaxLength(100)]public string Phone{get;set;}="";
 [Required,MaxLength(100)]public string Country{get;set;}="";
 [Required,MaxLength(100)]public string Subject{get;set;}="";
 [Required,MinLength(10),MaxLength(3000)]public string Message{get;set;}="";
 [Range(typeof(bool),"true","true")]public bool Consent{get;set;}
}
public class TripRequest{
 public DateOnly? TravelDate{get;set;}public bool FlexibleDates{get;set;}
 [Range(1,10000)]public int Travellers{get;set;}=2;
 [Range(1,3650)]public int DurationDays{get;set;}=10;
 [MaxLength(500)]public string TravelStyle{get;set;}="";
 public string[] Interests{get;set;}=[];
 [MaxLength(100)]public string Accommodation{get;set;}="";
 [Range(0,100000000)]public decimal? Budget{get;set;}
 [RegularExpression("^(EUR|GBP|CHF|USD|LKR)$")]public string Currency{get;set;}="EUR";
 [MaxLength(2000)]public string? SpecialRequirements{get;set;}
 [Required,MaxLength(150)]public string FullName{get;set;}="";
 [Required,EmailAddress]public string Email{get;set;}="";
 [Required,MaxLength(150)]public string Phone{get;set;}="";
 [Required,MaxLength(100)]public string Country{get;set;}="";
 [Range(typeof(bool),"true","true")]public bool PrivacyConsent{get;set;}
}
public class BookingRequest{
 [Required]public string TourSlug{get;set;}="";
 [Required]public DateOnly StartDate{get;set;}
 [Range(1,20)]public int Travellers{get;set;}=2;
 [Required,MaxLength(80)]public string FirstName{get;set;}="";
 [Required,MaxLength(80)]public string LastName{get;set;}="";
 [Required,EmailAddress]public string Email{get;set;}="";
 [MaxLength(70)]public string? Phone{get;set;}
 [MaxLength(2000)]public string? SpecialRequests{get;set;}
 [Range(typeof(bool),"true","true")]public bool PrivacyConsent{get;set;}
}
public class LoginRequest{[Required,EmailAddress]public string Email{get;set;}="";[Required]public string Password{get;set;}="";}
public class RegisterRequest{[Required]public string FirstName{get;set;}="";[Required]public string LastName{get;set;}="";[Required,EmailAddress]public string Email{get;set;}="";[Required,MinLength(12)]public string Password{get;set;}="";}
public record AuthResponse(string AccessToken,string Role,string DisplayName);
public interface IPaymentProvider {Task<string> CreateHostedSessionAsync(Guid bookingId,CancellationToken ct);Task<bool> VerifyWebhookAsync(string signature,string payload,CancellationToken ct);}
