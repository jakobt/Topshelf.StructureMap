using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Quartz;
using Quartz.Spi;
using StructureMap;

namespace Topshelf.Quartz.StructureMap
{
    public interface IOnJobInitializationFailedHandler
    {
        void Handle(Exception ex);
    }

    public class DefaultOnJobInitializationFailedHandler : IOnJobInitializationFailedHandler
    {
        public void Handle(Exception ex)
        {
            Debug.WriteLine("Exception on Starting job - {0}", (ex.InnerException ?? ex).Message);
        }
    }
    public class SimpleJobFactory : IJobFactory
	{
		private readonly IContainer _container;
        private readonly IOnJobInitializationFailedHandler _onjobinitializationfailedhandler;

        public SimpleJobFactory(IContainer container, IOnJobInitializationFailedHandler onjobinitializationfailedhandler) {
			_container = container;
            _onjobinitializationfailedhandler = onjobinitializationfailedhandler;
        }

		public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler) {
			IJob job;
			var jobDetail = bundle.JobDetail;
			var jobType = jobDetail.JobType;
			try {
				job = _container.GetInstance(jobType) as IJob;
			}
			catch (Exception ex) {
                    _onjobinitializationfailedhandler.Handle(ex);
                throw new SchedulerException(string.Format(CultureInfo.InvariantCulture,
                "Problem instantiating class '{0}'", jobDetail.JobType.FullName), ex);
            }
			return job;
		}

		public void ReturnJob(IJob job) {
		}
	}


	public class NestedContainerJobFactory : IJobFactory
	{
		private readonly IContainer _container;
		static readonly ConcurrentDictionary<int, IContainer> Containers = new ConcurrentDictionary<int, IContainer>();
        private readonly IOnJobInitializationFailedHandler _onjobinitializationfailedhandler;

        public NestedContainerJobFactory(IContainer container, IOnJobInitializationFailedHandler onjobinitializationfailedhandler)
        {
            _container = container;
            _onjobinitializationfailedhandler = onjobinitializationfailedhandler;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler) {
			IJob job;
			var jobDetail = bundle.JobDetail;
			var jobType = jobDetail.JobType;
			try {
				var nestedContainer = _container.GetNestedContainer();
				job = nestedContainer.GetInstance(jobType) as IJob;
				Containers.TryAdd(job.GetHashCode(), nestedContainer);
				Debug.WriteLine("Start job({1}) in thread - {0}. Containers count - {2}",
					Thread.CurrentThread.ManagedThreadId,
					job.GetHashCode(),
					Containers.Count);
			}
			catch (Exception ex) {
                _onjobinitializationfailedhandler.Handle(ex);
                throw new SchedulerException(string.Format(CultureInfo.InvariantCulture,
                "Problem instantiating class '{0}'", jobDetail.JobType.FullName), ex);
            }
			return job;
		}

		public void ReturnJob(IJob job) {
			if (job == null) {
				Debug.WriteLine("Job is null");
				return;
			}

			IContainer container;
			if (Containers.TryRemove(job.GetHashCode(), out container)) {
				if (container == null) { Debug.WriteLine("Container is null"); return; }
				container.Dispose();
			} else {
				Debug.WriteLine("Can't find ({0})", job.GetHashCode());
			}

			Debug.WriteLine("Return job({1}) in thread - {0}. Containers count - {2}",
				Thread.CurrentThread.ManagedThreadId,
				job.GetHashCode(),
				Containers.Count);
		}
	}
}