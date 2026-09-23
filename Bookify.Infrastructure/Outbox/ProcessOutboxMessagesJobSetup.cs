namespace Bookify.Infrastructure.Outbox
{
    using Microsoft.Extensions.Options;
    using Quartz;

    public class ProcessOutboxMessagesJobSetup : IConfigureOptions<QuartzOptions>
    {
        private readonly OutboxOptions outboxOptions;

        public ProcessOutboxMessagesJobSetup(IOptions<OutboxOptions> outboxOptions)
        {
            this.outboxOptions = outboxOptions.Value;
        }

        public void Configure(QuartzOptions options)
        {
            const string jobName = nameof(ProcessOutboxMessagesJob);

            options
                .AddJob<ProcessOutboxMessagesJob>(configure => configure.WithIdentity(jobName))
                .AddTrigger(configure =>
                    configure
                        .ForJob(jobName)
                        .WithSimpleSchedule(schedule =>
                            schedule.WithIntervalInSeconds(outboxOptions.IntervalInSeconds).RepeatForever()));
        }
    }
}