namespace MauiApp.Core.Models
{
    /// <summary>
    /// Represents the response from user information API
    /// </summary>
    public class UserInformationResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public bool UnAuthorizedRequest { get; set; }
        public UserInformationData? Result { get; set; }
    }

    /// <summary>
    /// User information data structure
    /// </summary>
    public class UserInformationData
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? EmailAddress { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public DateTime? CreationTime { get; set; }
        public string? FullName { get; set; }
        public string? DisplayName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? Department { get; set; }
        public string? JobTitle { get; set; }
        public string? Company { get; set; }
        public string? Notes { get; set; }
    }
}









