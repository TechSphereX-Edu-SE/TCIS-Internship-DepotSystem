using FluentValidation;
using TechSpherex.CleanArchitecture.Domain.Services;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Photos;

public sealed class UploadRepairPhotoValidator : AbstractValidator<UploadRepairPhotoCommand>
{
    public UploadRepairPhotoValidator()
    {
        RuleFor(x => x.CaseId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType)
            .Must(RepairPhotoRules.IsAllowedType)
            .WithMessage("Only JPG and PNG photos are accepted.");
        RuleFor(x => x.Length)
            .GreaterThan(0)
            .LessThanOrEqualTo(RepairPhotoRules.MaxBytes)
            .WithMessage("A photo must be at most 5 MB.");
    }
}
