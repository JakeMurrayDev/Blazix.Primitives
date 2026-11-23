namespace Iodine.Headless;

// TST-API-12 (ToastManagerPromiseOptions, useToastManager.ts:139-149; resolvePromiseOptions.ts:3-22): `loading` is a
// string or update options; `success` and `error` a string, update options, or a function of the value or the error that
// returns either. A string stands for `{ description }`, and update options are a toast whose initializer sets only the
// keys to change (ToastObject<TData>'s presence mask). ADR 08 Amendments 2026-11-18 (Promise<T> with these options,
// chosen by the user 2026-11-18); the names are M6 track 9a's.
/// <summary>Represents the three states of a toast that follows a task.</summary>
/// <typeparam name="TValue">The type of the task's result.</typeparam>
/// <typeparam name="TData">The type of the custom data the toast carries.</typeparam>
public class ToastPromiseOptions<TValue, TData>
{
    /// <summary>Gets the toast shown while the task runs.</summary>
    /// <value>A description, or a toast whose initializer sets the properties to show. Its type is always <c>"loading"</c>.</value>
    public required ToastPromiseState<TData> Loading { get; init; }

    /// <summary>Gets the change applied when the task completes.</summary>
    /// <value>
    /// A description, a toast whose initializer sets the properties to change, or a function of the result that returns
    /// either. The toast's type becomes <c>"success"</c>, and its timeout the one given here.
    /// </value>
    public required ToastPromiseState<TValue, TData> Success { get; init; }

    /// <summary>Gets the change applied when the task fails or is canceled.</summary>
    /// <value>
    /// A description, a toast whose initializer sets the properties to change, or a function of the exception that returns
    /// either. The toast's type becomes <c>"error"</c>, and its timeout the one given here.
    /// </value>
    public required ToastPromiseState<Exception, TData> Error { get; init; }
}

/// <summary>Represents the three states of a toast that follows a task and carries no custom data.</summary>
/// <typeparam name="TValue">The type of the task's result.</typeparam>
public sealed class ToastPromiseOptions<TValue> : ToastPromiseOptions<TValue, NoPayload>;

/// <summary>Represents the toast shown while a task runs: a description, or a toast with the properties to show.</summary>
/// <typeparam name="TData">The type of the custom data the toast carries.</typeparam>
public readonly struct ToastPromiseState<TData>
{
    private readonly string? description;
    private readonly ToastObject<TData>? toast;

    /// <summary>Initializes a new instance of the <see cref="ToastPromiseState{TData}"/> struct that shows a toast.</summary>
    /// <param name="toast">The toast whose initializer sets the properties to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="toast"/> is <see langword="null"/>.</exception>
    public ToastPromiseState(ToastObject<TData> toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        this.toast = toast;
    }

    private ToastPromiseState(string? description)
    {
        this.description = description;
    }

    /// <summary>Converts a description to a <see cref="ToastPromiseState{TData}"/> that shows it.</summary>
    /// <param name="description">The description.</param>
    /// <returns>A state that shows <paramref name="description"/> as the toast's description.</returns>
    public static implicit operator ToastPromiseState<TData>(string? description) => new(description);

    /// <summary>Converts a toast to a <see cref="ToastPromiseState{TData}"/> that shows it.</summary>
    /// <param name="toast">The toast whose initializer sets the properties to show.</param>
    /// <returns>A state that shows <paramref name="toast"/>.</returns>
    public static implicit operator ToastPromiseState<TData>(ToastObject<TData> toast) => new(toast);

    internal ToastObject<TData> Resolve() => toast ?? new ToastObject<TData> { Description = description };
}

/// <summary>
/// Represents the change applied to a toast when a task settles: a description, a toast with the properties to change,
/// or a function of the outcome that returns either.
/// </summary>
/// <typeparam name="TArg">The type of the outcome: the task's result, or the exception it failed with.</typeparam>
/// <typeparam name="TData">The type of the custom data the toast carries.</typeparam>
public readonly struct ToastPromiseState<TArg, TData>
{
    private readonly string? description;
    private readonly ToastObject<TData>? toast;
    private readonly Func<TArg, string?>? describe;
    private readonly Func<TArg, ToastObject<TData>>? change;

    /// <summary>Initializes a new instance of the <see cref="ToastPromiseState{TArg, TData}"/> struct that applies a toast.</summary>
    /// <param name="toast">The toast whose initializer sets the properties to change.</param>
    /// <exception cref="ArgumentNullException"><paramref name="toast"/> is <see langword="null"/>.</exception>
    public ToastPromiseState(ToastObject<TData> toast)
    {
        ArgumentNullException.ThrowIfNull(toast);
        this.toast = toast;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToastPromiseState{TArg, TData}"/> struct that describes the outcome.
    /// </summary>
    /// <param name="describe">The function, called once with the outcome, that returns the toast's description.</param>
    /// <exception cref="ArgumentNullException"><paramref name="describe"/> is <see langword="null"/>.</exception>
    public ToastPromiseState(Func<TArg, string?> describe)
    {
        ArgumentNullException.ThrowIfNull(describe);
        this.describe = describe;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToastPromiseState{TArg, TData}"/> struct that changes the toast from the
    /// outcome.
    /// </summary>
    /// <param name="change">
    /// The function, called once with the outcome, that returns a toast whose initializer sets the properties to change.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="change"/> is <see langword="null"/>.</exception>
    public ToastPromiseState(Func<TArg, ToastObject<TData>> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        this.change = change;
    }

    private ToastPromiseState(string? description)
    {
        this.description = description;
    }

    /// <summary>Converts a description to a <see cref="ToastPromiseState{TArg, TData}"/> that shows it.</summary>
    /// <param name="description">The description.</param>
    /// <returns>A state that shows <paramref name="description"/> as the toast's description.</returns>
    public static implicit operator ToastPromiseState<TArg, TData>(string? description) => new(description);

    /// <summary>Converts a toast to a <see cref="ToastPromiseState{TArg, TData}"/> that applies it.</summary>
    /// <param name="toast">The toast whose initializer sets the properties to change.</param>
    /// <returns>A state that applies <paramref name="toast"/>.</returns>
    public static implicit operator ToastPromiseState<TArg, TData>(ToastObject<TData> toast) => new(toast);

    internal ToastObject<TData> Resolve(TArg outcome)
    {
        if (change is not null)
        {
            return change(outcome) ?? new ToastObject<TData>();
        }

        if (toast is not null)
        {
            return toast;
        }

        return new ToastObject<TData> { Description = describe is not null ? describe(outcome) : description };
    }
}
