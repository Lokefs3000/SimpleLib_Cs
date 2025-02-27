using Schedulers;
using SimpleEditor.Files;
using SimpleEditor.Import.Processors;
using SimpleEditor.Runtime;

namespace SimpleEditor.Import
{
    public class Importer : IDisposable
    {
        private readonly EditorRuntime _edr;
        private readonly JobScheduler _scheduler;

        private Dictionary<ProjectFileSystem.FileType, IJob> _jobs = new Dictionary<ProjectFileSystem.FileType, IJob>();

        public Importer(EditorRuntime edr)
        {
            _edr = edr;
            _scheduler = edr.JobScheduler;

            _jobs.Add(ProjectFileSystem.FileType.Image, new ImageImporter());
            _jobs.Add(ProjectFileSystem.FileType.Model, new ModelImporter());
        }

        public void Dispose()
        {

        }

        public void ImportIfOld(ProjectFileSystem fs, ProjectFileSystem.FileData data)
        {
            DateTime realLastWrite = File.GetLastWriteTime(data.RealPath);
            DateTime cachedLastWrite = File.GetLastWriteTime(data.FullPath);

            if (realLastWrite > cachedLastWrite)
            {
                IJob importer = _jobs[data.Type];
                switch (data.Type)
                {
                    case ProjectFileSystem.FileType.Image:
                        {
                            ImageImporter.Arguments args = new ImageImporter.Arguments();
                            args.EdRuntime = _edr;
                            args.Output = data.FullPath;
                            args.Id = data.Id;

                            lock (ImageImporter.Pending)
                            {
                                ImageImporter.Pending.Enqueue(args);
                            }

                            _scheduler.Schedule(importer);

                            break;
                        }
                    case ProjectFileSystem.FileType.Model:
                        {
                            ModelImporter.Arguments args = new ModelImporter.Arguments();
                            args.EDR = _edr;
                            args.Output = data.FullPath;
                            args.Id = data.Id;
                            args.Hint = Path.GetExtension(data.RealPath).Substring(1);

                            lock (ModelImporter.Pending)
                            {
                                ModelImporter.Pending.Enqueue(args);
                            }

                            _scheduler.Schedule(importer);

                            break;
                        }
                    default: break;
                }
            }
        }
    }
}
