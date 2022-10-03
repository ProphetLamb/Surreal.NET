using System.ComponentModel.DataAnnotations;

namespace BlazorSurveys.Shared; 

public class AddSurveyModel: IValidatableObject
{
    [Required]
    [MaxLength(50)]
    public string Title { get; set; }
    [Required]
    public int? Minutes { get; set; }

    // Note for Blazor to validate nested complex type, this annotation is required
    // which is defined in the NuGet package Microsoft.AspNetCore.Components.DataAnnotations.Validation, version 3.2
    // and it is still in "experimental mode"
    // See https://docs.microsoft.com/en-us/aspnet/core/blazor/forms-validation?view=aspnetcore-5.0#nested-models-collection-types-and-complex-types
    [ValidateComplexType]
    public List<OptionCreateModel> Options { get; init; } = new();

    public void RemoveOption(OptionCreateModel option) => Options.Remove(option);

    public void AddOption() => Options.Add(new OptionCreateModel());

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Options.Count < 2 )
        {
            yield return new ValidationResult("A survey requires at least 2 options.");
        }
    }
}

public class OptionCreateModel
{
    [Required]
    [MaxLength(20)]
    public string OptionValue { get; set; }
}
