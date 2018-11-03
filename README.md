# Open.ChannelExtensions

A set of extensions for optimizing/simplifying System.Threading.Channels usage.

## Highlights

Being able to define an asynchronous pipeline with best practice usage using simple expressive syntax:

```cs
await Channel
    .CreateBounded<T>(10)
    .SourceAsync(source /* IEnumerable<Task<T>> */)
    .PipeAsync(asyncTransform01, 5),
    .Pipe(transform02, 2)
    .ReadAllAsync(e=>{
        // Do something.
    });
```

## Examples

### Reading

#### One by one read each entry from the channel

```cs
await channel.ReadAll(entry => { /* Processing Code */ });
```

```cs
await channel.ReadAll((entry,index) => { /* Processing Code */ });
```

```cs
await channel.ReadAllAsync(async entry => { await /* Processing Code */ });
```

```cs
await channel.ReadAllAsync(async (entry,index) => { await /* Processing Code */ });
```

### Writing

#### Dump a source enumeration into the channel

```cs
// source can be any IEnumerable<T>.
await channel.WriteAll(source, complete: true);
```

```cs
// source can be any IEnumerable<Task<T>> or IEnumerable<ValueTask<T>>.
await channel.WriteAllAsync(source, complete: true);
```

### Pipelining / Transforming

#### Transform and buffer entries

```cs
// Transform values in a source channel to new unbounded channel.
var transformed = channel.Pipe(async value => /* transformation */);
```

```cs
// Transform values in a source channel to new bounded channel bound of N entries.
const N = 5;
var transformed = channel.Pipe(async value => /* transformation */, N);
```
