using EventFlow.Domain.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlow.Application.Common;

// CQRS leve: comandos alteram dados, consultas só leem. Cada um tem um handler.

public interface IRequest<TResult>;
public interface ICommand<TResult> : IRequest<TResult>;
public interface IQuery<TResult> : IRequest<TResult>;

public interface IRequestHandler<in TRequest, TResult> where TRequest : IRequest<TResult>
{
    Task<Result<TResult>> Handle(TRequest request, CancellationToken ct);
}

/// <summary>Encaminha comandos e consultas ao handler certo, depois de validar.</summary>
public interface IDispatcher
{
    Task<Result<TResult>> Send<TResult>(IRequest<TResult> request, CancellationToken ct = default);
}

internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    public Task<Result<TResult>> Send<TResult>(IRequest<TResult> request, CancellationToken ct = default)
    {
        var pipelineType = typeof(Pipeline<,>).MakeGenericType(request.GetType(), typeof(TResult));
        var pipeline = (PipelineBase<TResult>)Activator.CreateInstance(pipelineType)!;
        return pipeline.Run(request, services, ct);
    }

    private abstract class PipelineBase<TResult>
    {
        public abstract Task<Result<TResult>> Run(IRequest<TResult> request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class Pipeline<TRequest, TResult> : PipelineBase<TResult> where TRequest : IRequest<TResult>
    {
        public override async Task<Result<TResult>> Run(IRequest<TResult> request, IServiceProvider services, CancellationToken ct)
        {
            var typed = (TRequest)request;

            var failures = new List<FluentValidation.Results.ValidationFailure>();
            foreach (var validator in services.GetServices<IValidator<TRequest>>())
                failures.AddRange((await validator.ValidateAsync(typed, ct)).Errors);

            if (failures.Count > 0)
            {
                var fields = failures
                    .GroupBy(f => ToCamelCase(f.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());
                return Error.Validation(failures[0].ErrorMessage, fields);
            }

            try
            {
                return await services.GetRequiredService<IRequestHandler<TRequest, TResult>>().Handle(typed, ct);
            }
            catch (DomainException ex)
            {
                return Error.Rule(ex.Message);
            }
        }

        private static string ToCamelCase(string name) =>
            string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }
}
