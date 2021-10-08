# Open.ChannelExtensions

[![NuGet](https://img.shields.io/nuget/v/Open.ChannelExtensions.svg?style=flat)](https://www.nuget.org/packages/Open.ChannelExtensions/)

A set of extensions for optimizing/simplifying System.Threading.Channels usage.

[Click here for detailed documentation.](https://open-net-libraries.github.io/Open.ChannelExtensions/api/Open.ChannelExtensions.Extensions.html#methods)

## Highlights

### Read & Write

*With optional concurrency levels.*

* Reading all entries in a channel.
* Writing all entries from a source to a channel.
* Piping (consuming) all entries to a buffer (channel).
* `.AsAsyncEnumerable()` (`IAsyncEnumerable`) support for .NET Standard 2.1+ and .NET Core 3+

### Special `ChannelReader` Operations

* `Filter`
* `Transform`
* `Batch`
* `Join`

---

## Examples

Being able to define an asynchronous pipeline with best practice usage using simple expressive syntax:

```cs
await Channel
    .CreateBounded<T>(10)
    .SourceAsync(source /* IEnumerable<Task<T>> */)
    .PipeAsync(
        maxConcurrency: 2,
        capacity: 5,
        transform: asyncTransform01)
    .Pipe(transform02, /* capacity */ 3)
    .ReadAllAsync(finalTransformedValue => {
        // Do something async with each final value.
    });
```

```cs
await source /* IEnumerable<T> */
    .ToChannel(boundedSize: 10, singleReader: true)
    .PipeAsync(asyncTransform01, /* capacity */ 5)
    .Pipe(
        maxConcurrency: 2,
        capacity: 3,
        transform: transform02)
    .ReadAll(finalTransformedValue => {
        // Do something with each final value.
    });
```

### Reading (until the channel is closed)

#### One by one read each entry from the channel

```cs
await channel.ReadAll(
    entry => { /* Processing Code */ });
```

```cs
await channel.ReadAll(
    (entry, index) => { /* Processing Code */ });
```

```cs
await channel.ReadAllAsync(
    async entry => { await /* Processing Code */ });
```

```cs
await channel.ReadAllAsync(
    async (entry, index) => { await /* Processing Code */ });
```

#### Read concurrently each entry from the channel

```cs
await channel.ReadAllConcurrently(
    maxConcurrency,
    entry => { /* Processing Code */ });
```

```cs
await channel.ReadAllConcurrentlyAsync(
    maxConcurrency,
    async entry => { await /* Processing Code */ });
```

### Writing

If `complete` is `true`, the channel will be closed when the source is empty.

#### Dump a source enumeration into the channel

```cs
// source can be any IEnumerable<T>.
await channel.WriteAll(source, complete: true);
```

```cs
// source can be any IEnumerable<Task<T>> or IEnumerable<ValueTask<T>>.
await channel.WriteAllAsync(source, complete: true);
```

#### Synchronize reading from the source and process the results concurrently

```cs
// source can be any IEnumerable<Task<T>> or IEnumerable<ValueTask<T>>.
await channel.WriteAllConcurrentlyAsync(
    maxConcurrency, source, complete: true);
```

### Filter & Transform

```cs
// Filter and transform when reading.
channel.Reader
    .Filter(predicate) // .Where()
    .Transform(selector) // .Select()
    .ReadAllAsync(async value => {/*...*/});
```

### Batching

```cs
values.Reader
    .Batch(10 /*batch size*/)
    .WithTimeout(1000) // Any non-empty batches are flushed every second.
    .ReadAllAsync(async batch => {/*...*/});
```

### Joining

```cs
batches.Reader
    .Join()
    .ReadAllAsync(async value => {/*...*/});
```

### Pipelining / Transforming

#### Transform and buffer entries

```cs
// Transform values in a source channel to new unbounded channel.
var transformed = channel.Pipe(
    async value => /* transformation */);
```

```cs
// Transform values in a source channel to new unbounded channel with a max concurrency of X.
const X = 4;
var transformed = channel.Pipe(
    X, async value => /* transformation */);
```

```cs
// Transform values in a source channel to new bounded channel bound of N entries.
const N = 5;
var transformed = channel.Pipe(
    async value => /* transformation */, N);
```

```cs
// Transform values in a source channel to new bounded channel bound of N entries with a max concurrency of X.
const X = 4;
const N = 5;
var transformed = channel.Pipe(
    X, async value => /* transformation */, N);

// or
transformed = channel.Pipe(
    maxConcurrency: X,
    capacity: N,
    transform: async value => /* transformation */);
```
