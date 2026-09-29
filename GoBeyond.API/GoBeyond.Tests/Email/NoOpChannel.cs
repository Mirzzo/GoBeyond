using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GoBeyond.Tests.Email;

/// <summary>
/// Minimalan <see cref="IChannel"/> "stub" korišten SAMO da se napravi pravi <see cref="AsyncEventingBasicConsumer"/>
/// u WorkerConsumerLoopTests (konstruktor zahtijeva IChannel, ali ga ne poziva pri konstrukciji). Svaki član
/// baca ili vraća bezopasnu podrazumijevanu vrijednost jer testovi NIKAD ne izvode pravu operaciju preko
/// kanala - zovu direktno consumer-ove Handle*Async metode (HandleBasicCancelAsync / HandleChannelShutdownAsync),
/// iste metode koje RabbitMQ.Client-ova dispatch petlja zove kad broker otkaže consumer-a (npr. obrisan queue)
/// ili kanal/konekcija padne.
/// </summary>
internal sealed class NoOpChannel : IChannel
{
    public int ChannelNumber => 1;
    public ShutdownEventArgs? CloseReason => null;
    public IAsyncBasicConsumer? DefaultConsumer { get; set; }
    public bool IsClosed => false;
    public bool IsOpen => true;
    public string? CurrentQueue => null;
    public TimeSpan ContinuationTimeout { get; set; }

    public event AsyncEventHandler<BasicAckEventArgs>? BasicAcksAsync { add { } remove { } }
    public event AsyncEventHandler<BasicNackEventArgs>? BasicNacksAsync { add { } remove { } }
    public event AsyncEventHandler<BasicReturnEventArgs>? BasicReturnAsync { add { } remove { } }
    public event AsyncEventHandler<CallbackExceptionEventArgs>? CallbackExceptionAsync { add { } remove { } }
    public event AsyncEventHandler<FlowControlEventArgs>? FlowControlAsync { add { } remove { } }
    public event AsyncEventHandler<ShutdownEventArgs>? ChannelShutdownAsync { add { } remove { } }

    private static NotImplementedException NotUsed() =>
        new("NoOpChannel: not implemented - WorkerConsumerLoopTests never performs a real channel operation.");

    public ValueTask<ulong> GetNextPublishSequenceNumberAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public ValueTask BasicAckAsync(ulong deliveryTag, bool multiple, CancellationToken cancellationToken = default) => throw NotUsed();
    public ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task BasicCancelAsync(string consumerTag, bool noWait = false, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<string> BasicConsumeAsync(string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object?>? arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<BasicGetResult?> BasicGetAsync(string queue, bool autoAck, CancellationToken cancellationToken = default) => throw NotUsed();
    public ValueTask BasicPublishAsync<TProperties>(string exchange, string routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default) where TProperties : IReadOnlyBasicProperties, IAmqpHeader => throw NotUsed();
    public ValueTask BasicPublishAsync<TProperties>(CachedString exchange, CachedString routingKey, bool mandatory, TProperties basicProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default) where TProperties : IReadOnlyBasicProperties, IAmqpHeader => throw NotUsed();
    public Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global, CancellationToken cancellationToken = default) => throw NotUsed();
    public ValueTask BasicRejectAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task CloseAsync(ushort replyCode, string replyText, bool abort, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task CloseAsync(ShutdownEventArgs reason, bool abort) => throw NotUsed();
    public Task CloseAsync(ShutdownEventArgs reason, bool abort, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<uint> ConsumerCountAsync(string queue, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task ExchangeBindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task ExchangeDeclareAsync(string exchange, string type, bool durable, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task ExchangeDeclarePassiveAsync(string exchange, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task ExchangeDeleteAsync(string exchange, bool ifUnused = false, bool noWait = false, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task ExchangeUnbindAsync(string destination, string source, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<uint> MessageCountAsync(string queue, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task QueueBindAsync(string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments = null, bool noWait = false, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<QueueDeclareOk> QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null, bool passive = false, bool noWait = false, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<QueueDeclareOk> QueueDeclarePassiveAsync(string queue, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<uint> QueueDeleteAsync(string queue, bool ifUnused, bool ifEmpty, bool noWait = false, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task<uint> QueuePurgeAsync(string queue, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task QueueUnbindAsync(string queue, string exchange, string routingKey, IDictionary<string, object?>? arguments = null, CancellationToken cancellationToken = default) => throw NotUsed();
    public Task TxCommitAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task TxRollbackAsync(CancellationToken cancellationToken = default) => throw NotUsed();
    public Task TxSelectAsync(CancellationToken cancellationToken = default) => throw NotUsed();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Dispose() { }
}
