using PatchMindAI.Core.Contracts;

namespace PatchMindAI.Tests.Unit.Contracts;

public class CreateAnalysisJobRequestValidationTests
{
    [Fact]
    public void CveId_ShouldBeValid_WithCorrectFormat()
    {
        // Arrange & Act
        var request = new CreateAnalysisJobRequest
        {
            CveId = "CVE-2021-44228",
            UserQuery = "Test query"
        };

        // Assert - no exception thrown during validation
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, context, results, true);
        
        Assert.True(isValid);
    }

    [Fact]
    public void CveId_ShouldBeInvalid_WithIncorrectFormat()
    {
        // Arrange & Act
        var request = new CreateAnalysisJobRequest
        {
            CveId = "INVALID-CVE",
            UserQuery = "Test query"
        };

        // Assert
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, context, results, true);
        
        Assert.False(isValid);
        Assert.NotEmpty(results);
    }

    [Theory]
    [InlineData("CVE-2021-44228")]
    [InlineData("CVE-2023-123456")]
    [InlineData("CVE-2000-1234")]
    public void CveId_ShouldAcceptValidFormats(string cveId)
    {
        // Arrange & Act
        var request = new CreateAnalysisJobRequest
        {
            CveId = cveId,
            UserQuery = "Test"
        };

        // Assert
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, context, results, true);
        
        Assert.True(isValid);
    }

    [Fact]
    public void CveId_ShouldBeRequired()
    {
        // Arrange & Act
        var request = new CreateAnalysisJobRequest
        {
            CveId = "",
            UserQuery = "Test"
        };

        // Assert
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, context, results, true);
        
        Assert.False(isValid);
    }

    [Fact]
    public void UserQuery_ShouldBeOptional()
    {
        // Arrange & Act
        var request = new CreateAnalysisJobRequest
        {
            CveId = "CVE-2021-44228",
            UserQuery = null
        };

        // Assert
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(request);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(request, context, results, true);
        
        Assert.True(isValid);
    }
}
