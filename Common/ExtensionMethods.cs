﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Config;
using Common.Localization.Providers;
using Common.Music.Tracks;
using Common.Utils;
using Discord;
using Lavalink4NET.Artwork;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Configuration;

namespace Common;

public static class ExtensionMethods {
    public static void DelayedDelete(this IMessage message, TimeSpan span) {
        _ = Task.Delay(span).ContinueWith(task => message.SafeDelete());
    }

    /// <returns>
    /// Original <paramref name="messageTask"/>
    /// </returns>
    public static Task DelayedDelete<T>(this Task<T> messageTask, TimeSpan span) where T : IMessage {
        _ = Task.Delay(span).ContinueWith(task => messageTask.SafeDelete());
        return messageTask;
    }

    /// <returns>
    /// Task, which completed when target message was deleted
    /// </returns>
    public static Task DelayedDeleteAsync<T>(this Task<T> messageTask, TimeSpan span) where T : IMessage {
        return Task.Delay(span).ContinueWith(task => messageTask.SafeDelete());
    }

    public static void SafeDelete<T>(this Task<T> message) where T : IMessage {
        try {
            message.ContinueWith(async task => {
                try {
                    await (await task).SafeDeleteAsync();
                }
                catch (Exception) {
                    // ignored
                }
            });
        }
        catch (Exception) {
            //-V3163
            // ignored
        }
    }

    public static void SafeDelete<T>(this T? message) where T : IMessage {
        try {
            message?.DeleteAsync().ObserveException();
        }
        catch (Exception) {
            //-V3163
            // ignored
        }
    }

    public static async ValueTask SafeDeleteAsync<T>(this T? message) where T : IMessage {
        try {
            if (message == null) return;
            await message.DeleteAsync();
        }
        catch (Exception) {
            // ignored
        }
    }

    public static string Format(this string format, params object?[] args) {
        return string.Format(format, args);
    }

    public static string? SafeSubstring(this string? text, int start, int length) {
        if (text == null) return null;

        return text.Length <= start ? ""
            : text.Length - start <= length ? text.Substring(start)
            : text.Substring(start, length);
    }

    public static string? SafeSubstring(this ReadOnlySpan<char> text, int length, string postContent = "") {
        return text.Length <= length
            ? text.ToString()
            : text[..Math.Min(text.Length, length - postContent.Length)].ToString() + postContent;
    }

    [return: NotNullIfNotNull("text")]
    public static string? SafeSubstring(this string? text, int length, string postContent = "") {
        return text?.AsSpan().SafeSubstring(length, postContent);
    }

    public static string Repeat(this string s, int count) {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (count <= 0) return string.Empty;
        var builder = new StringBuilder(s.Length * count);

        for (var i = 0; i < count; i++) builder.Append(s);

        return builder.ToString();
    }

    public static T Next<T>(this T src) where T : struct {
        if (!typeof(T).IsEnum) throw new ArgumentException($"Argument {typeof(T).FullName} is not an Enum");

        var arr = (T[])Enum.GetValues(src.GetType());
        var j = Array.IndexOf(arr, src) + 1;
        return arr.Length == j ? arr[0] : arr[j];
    }

    public static int Normalize(this int value, int min, int max) {
        return Math.Max(min, Math.Min(max, value));
    }

    // ReSharper disable once InconsistentNaming
    public static async Task<IMessage> SendTextAsFile(this IMessageChannel channel, string content, string filename,
        string? text = null,
        bool isTTS = false,
        Embed? embed = null, RequestOptions? options = null, bool isSpoiler = false) {
        await using var ms = new MemoryStream();
        TextWriter tw = new StreamWriter(ms);
        await tw.WriteAsync(content);
        await tw.FlushAsync();
        ms.Position = 0;
        return await channel.SendFileAsync(ms, filename);
    }

    public static TResult Try<TSource, TResult>(this TSource o, Func<TSource, TResult> action,
        Func<TSource, TResult> onFail) {
        try {
            return action(o);
        }
        catch {
            return onFail(o);
        }
    }

    public static TResult Try<TSource, TResult>(this TSource o, Func<TSource, TResult> action, TResult onFail) {
        try {
            return action(o);
        }
        catch {
            return onFail;
        }
    }

    public static LocalizationContainer ToContainer(this ILocalizationProvider provider) {
        if (provider is LocalizationContainer localizationContainer) return localizationContainer;
        return new LocalizationContainer(provider);
    }

    /// <summary>
    /// Extension method for fast string validation. WARN: Actually the IsNullOrWhiteSpace method is implied
    /// </summary>
    public static bool IsBlank(this string? source) {
        return string.IsNullOrWhiteSpace(source);
    }

    /// <summary>
    /// Extension method for fast string getting. WARN: Actually the IsNullOrWhiteSpace method is implied
    /// </summary>
    /// <param name="source">Source string</param>
    /// <param name="replacement">Replacement</param>
    /// <returns>If target string is null or whitespace - return <paramref name="replacement"/>. Otherwise - return <paramref name="source"/></returns>
    public static string IsBlank(this string? source, string replacement) {
        return string.IsNullOrWhiteSpace(source) ? replacement : source;
    }

    public static string FormattedToString(this TimeSpan span) {
        string s = $"{span:mm':'ss}";
        if ((int)span.TotalHours != 0)
            s = s.Insert(0, $"{(int)span.TotalHours}:");
        return s;
    }

    public static UserLink ToLink(this IUser user) {
        return new UserLink(user.Id);
    }

    public static TimeSpan Sum<TSource>(this IEnumerable<TSource> source, Func<TSource, TimeSpan> func) {
        return new TimeSpan(source.Sum(item => func(item).Ticks));
    }

    public static void ShouldDispose(this IDisposableBase disposableBase, IDisposable disposable) {
        if (disposableBase.IsDisposed) {
            disposable.Dispose();
        }
        else {
            _ = disposableBase.WaitForDisposeAsync().ContinueWith(_ => disposable.Dispose());
        }
    }

    public static string GetLocalizedContent(this Exception exception, ILocalizationProvider provider) {
        if (exception is LocalizedException localizedException) {
            return localizedException.Get(provider);
        }

        return exception.Message;
    }

    public static string Or(this string? source, string replacement) {
        if (string.IsNullOrWhiteSpace(source)) {
            return replacement;
        }

        return source;
    }

    public static TOut Pipe<TIn, TOut>(this TIn input, Func<TIn, TOut> transform) => transform(input);

    public static async Task<TOut> PipeAsync<TIn, TOut>(this Task<TIn> input, Func<TIn, TOut> transform) =>
        transform(await input);

    public static async Task<TOut> PipeAsync<TIn, TOut>(this Task<TIn> input, Func<TIn, Task<TOut>> transform) =>
        await transform(await input);

    public static async Task PipeAsync<TIn>(this Task<TIn> input, Action<TIn> transform) => transform(await input);

    public static async ValueTask<TOut> PipeAsync<TIn, TOut>(this ValueTask<TIn> input, Func<TIn, TOut> transform) =>
        transform(await input);

    public static async ValueTask<TOut>
        PipeAsync<TIn, TOut>(this ValueTask<TIn> input, Func<TIn, Task<TOut>> transform) =>
        await transform(await input);

    public static IEnumerable<Task<TOut>> PipeEveryAsync<TIn, TOut>(this IEnumerable<Task<TIn>> tasks,
        Func<TIn, TOut> transform) => tasks.Select(async task => transform(await task));

    public static IEnumerable<Task<TOut>> PipeEveryAsync<TIn, TOut>(this IEnumerable<Task<TIn>> tasks,
        Func<TIn, Task<TOut>> transform) => tasks.Select(async task => await transform(await task));

    /// <summary>
    /// Фильтрует элементы, у которых указанное поле !=null
    /// </summary>
    /// <remarks> Заменяет вызов типа
    /// entities.Where(x => x.SampleProperty is not null).Select(x => x.SampleProperty!)
    /// на entities.WhereNotNull(x => x.SampleProperty)
    /// </remarks>
    /// <param name="enumerable"> Перечисление </param>
    /// <param name="selector"> Селектор поля </param>
    /// <typeparam name="TSource"> Тип элемента </typeparam>
    /// <typeparam name="TField"> Тип поля </typeparam>
    /// <returns> Коллекцию элементов, у которых поле <typeparamref name="TField"/> != null </returns>
    public static IEnumerable<TField> WhereNotNull<TSource, TField>(
        this IEnumerable<TSource> enumerable,
        Func<TSource, TField?> selector)
        where TSource : class
        where TField : class => enumerable
        .Select(selector)
        .Where(x => x is not null)!;

    /// <summary>
    /// Фильтрует элементы, у которых указанное поле !=null
    /// </summary>
    /// <remarks> Заменяет вызов типа
    /// entities.Where(x => x.ExternalId.HasValue).Select(x => x.ExternalId!.Value)
    /// на entities.WhereNotNull(x => x.ExternalId)
    /// </remarks>
    /// <param name="enumerable"> Перечисление </param>
    /// <param name="selector"> Селектор поля </param>
    /// <typeparam name="TSource"> Тип элемента </typeparam>
    /// <typeparam name="TField"> Тип поля </typeparam>
    /// <returns> Коллекцию элементов, у которых поле <typeparamref name="TField"/> != null </returns>
    public static IEnumerable<TField> WhereNotNull<TSource, TField>(
        this IEnumerable<TSource> enumerable,
        Func<TSource, TField?> selector)
        where TSource : class
        where TField : struct => enumerable
        .Select(selector)
        .WhereNotNull();

    /// <summary>
    /// Фильтрует элементы, которые != null
    /// </summary>
    /// <param name="enumerable">Перечисление</param>
    /// <typeparam name="T">Тип элемента</typeparam>
    /// <returns>Коллекцию элементов, которые != null</returns>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> enumerable)
        where T : class => enumerable
        .Where(arg => arg is not null)
        .Select(arg => arg!);

    /// <summary>
    /// Фильтрует элементы, которые != null
    /// </summary>
    /// <param name="enumerable">Перечисление</param>
    /// <typeparam name="T">Тип элемента</typeparam>
    /// <returns>Коллекцию элементов, которые != null</returns>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> enumerable)
        where T : struct => enumerable
        .Where(arg => arg.HasValue)
        .Select(arg => arg!.Value);

    public static Task WhenAll(this IEnumerable<Task> tasks) => Task.WhenAll(tasks);
    public static Task<T[]> WhenAll<T>(this IEnumerable<Task<T>> tasks) => Task.WhenAll(tasks);

    public static async ValueTask<T[]> WhenAll<T>(this IEnumerable<ValueTask<T>> tasksEnumerable) {
        var tasks = tasksEnumerable.ToArray();
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Length == 0)
            return Array.Empty<T>();

        // We don't allocate the list if no task throws
        List<Exception>? exceptions = null;

        var results = new T[tasks.Length];
        for (var i = 0; i < tasks.Length; i++) {
            try {
                results[i] = await tasks[i].ConfigureAwait(false);
            }
            catch (Exception ex) {
                exceptions ??= new(tasks.Length);
                exceptions.Add(ex);
            }
        }

        return exceptions is null
            ? results
            : throw new AggregateException(exceptions);
    }

    public static async ValueTask WhenAll(this IEnumerable<ValueTask> tasksEnumerable) {
        var tasks = tasksEnumerable
            .Where(task => !task.IsCompleted)
            .ToArray();
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Length == 0)
            return;

        // We don't allocate the list if no task throws
        List<Exception>? exceptions = null;

        for (var i = 0; i < tasks.Length; i++)
            try {
                await tasks[i].ConfigureAwait(false);
            }
            catch (Exception ex) {
                exceptions ??= new List<Exception>(tasks.Length);
                exceptions.Add(ex);
            }

        if (exceptions is not null)
            throw new AggregateException(exceptions);
    }

    public static async Task<IDisposable> WaitDisposableAsync(this SemaphoreSlim semaphore,
        CancellationToken? token = null) {
        await semaphore.WaitAsync(token ?? CancellationToken.None);
        return Disposable.Create(() => semaphore.Release());
    }

    public static IEnumerable<T> DequeueExisting<T>(this ConcurrentQueue<T> queue) {
        while (queue.TryDequeue(out var item))
            yield return item;
    }

    public static IDisposable SubscribeAsync<T>(this IObservable<T> observable, Func<T, Task> action) {
        return observable
            .Select(arg => Observable.FromAsync(() => action(arg)))
            .Concat()
            .Subscribe();
    }

    public static async Task WhenEnd(this Task tsk) {
        try {
            await tsk;
        }
        catch (Exception) {
            // ignored
        }
    }

    public static void Do<T>(this IEnumerable<T> sequence, Action<T> action) {
        using var enumerator = sequence.GetEnumerator();
        while (enumerator.MoveNext()) action(enumerator.Current);
    }

    public static async Task ObserveException(this Task task) {
        if (task.IsCompleted) {
            return;
        }

        try {
            await task;
        }
        catch (Exception) {
            // ignored
        }
    }

    public static IEnumerator<T> GetEnumerator<T>(this IEnumerator<T> enumerator) => enumerator;

    public static T? GetSectionValue<T>(this IConfiguration configuration, string key) {
        return configuration.GetSection(key).Get<T>();
    }

    public static Uri Append(this Uri uri, params string[] paths) {
        return new Uri(paths.Aggregate(uri.AbsoluteUri,
            (current, path) => string.Format("{0}/{1}", current.TrimEnd('/'), path.TrimStart('/'))));
    }

    [return: NotNullIfNotNull("input")]
    public static string? RemoveNonPrintableChars(this string? input) {
        if (input is null) return null;

        return string.Create(input.Length, input, (span, s) => {
            s.CopyTo(span);
            for (var i = 0; i < span.Length; i++) {
                switch (span[i]) {
                    case '\'':
                    case '"':
                    case '#':
                    case '`':
                        span[i] = ' ';
                        break;
                }
            }
        });
    }

    public static IEnlivenQueueItem? GetSelectedTrackInPlaylist(this TrackLoadResult result,
        IReadOnlyList<IEnlivenQueueItem> list) {
        var playlistSelectedTrack = result.Playlist?.SelectedTrack;
        if (playlistSelectedTrack is null) {
            return null;
        }

        var index = result.Tracks.IndexOf(playlistSelectedTrack);
        return list[index];
    }

    public static async ValueTask<Uri?> ResolveArtwork(this LavalinkTrack track, IArtworkService artworkService) {
        return track is ITrackHasArtwork trackHasArtwork
            ? await trackHasArtwork.GetArtwork()
            : await artworkService.ResolveAsync(track);
    }

    public static string Format(this TrackException exception) {
        var text = "Unknown exception";
        if (exception.Cause is not null) {
            text = exception.Message is not null
                ? $"{exception.Message} ({exception.Cause})"
                : exception.Cause;
        }

        return $"[{exception.Severity}] {text}";
    }
}