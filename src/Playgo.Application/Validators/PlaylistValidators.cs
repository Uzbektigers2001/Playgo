using FluentValidation;
using Playgo.Application.DTOs.UserActivity;

namespace Playgo.Application.Validators;

public class CreatePlaylistRequestValidator : AbstractValidator<CreatePlaylistRequest>
{
    public CreatePlaylistRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdatePlaylistRequestValidator : AbstractValidator<UpdatePlaylistRequest>
{
    public UpdatePlaylistRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class AddItemToPlaylistRequestValidator : AbstractValidator<AddItemToPlaylistRequest>
{
    public AddItemToPlaylistRequestValidator()
    {
        RuleFor(x => x.ContentId).NotEmpty();
    }
}

public class AddToWatchlistRequestValidator : AbstractValidator<AddToWatchlistRequest>
{
    public AddToWatchlistRequestValidator()
    {
        RuleFor(x => x.ContentId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
