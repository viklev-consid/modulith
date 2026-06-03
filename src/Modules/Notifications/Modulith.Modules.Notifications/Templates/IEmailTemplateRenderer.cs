using ErrorOr;

namespace Modulith.Modules.Notifications.Templates;

public interface IEmailTemplateRenderer
{
    ErrorOr<RenderedEmailTemplate> Render<TModel>(EmailTemplateId templateId, TModel model)
        where TModel : notnull;
}
