using ReactiveUI;
using System.Application.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace System.Application.UI.ViewModels
{
    public class ExplorerViewModel : ViewModelBase
    {
        const double unit = 1024d;
        static readonly string[] units = new[] { "B", "KB", "MB", "GB", "TB" };
        public static string GetSize(double length)
        {
            for (int i = 0; i < units.Length; i++)
            {
                if (i > 0) length /= unit;
                if (length < unit) return $"{length:0.00} {units[i]}";
            }
            return $"{length:0.00} {units.Last()}";
        }

        string _CurrentPath = string.Empty;
        public string CurrentPath
        {
            get => _CurrentPath;
            set
            {
                if (_CurrentPath == value) return;
                _CurrentPath = value;
                var title = DefaultTitle;
                foreach (var item in GetRootPaths())
                {
                    if (_CurrentPath.StartsWith(item.Key))
                    {
                        title = Path.DirectorySeparatorChar +
                            item.Value +
                            _CurrentPath.TrimStart(item.Key);
                        break;
                    }
                }
                Title = title;
                foreach (var item in PathInfos.ToArray())
                {
                    PathInfos.Remove(item);
                }
                if (string.IsNullOrEmpty(_CurrentPath))
                {
                    PathInfos.AddRange(GetRootPathInfoViewModels<List<PathInfoViewModel>>());
                }
                else
                {
                    AddRange(PathInfos, _CurrentPath);
                }
            }
        }

        static readonly string DefaultTitle = Path.DirectorySeparatorChar.ToString();

        string title = DefaultTitle;
        public string Title
        {
            get => title;
            set => this.RaiseAndSetIfChanged(ref title, value);
        }

        public ObservableCollection<PathInfoViewModel> PathInfos { get; } = GetRootPathInfoViewModels<ObservableCollection<PathInfoViewModel>>();

        public class PathInfoViewModel : ReactiveObject
        {
            public PathInfoViewModel(FileSystemInfo fileSystemInfo, string? name = null)
            {
                Name = name ?? fileSystemInfo.Name;
                FullName = fileSystemInfo.FullName;
                Desc = $" | {fileSystemInfo.CreationTime.ToString(DateTimeFormat.Standard)}";
                if (fileSystemInfo is FileInfo fileInfo)
                {
                    IsDirectory = false;
                    var fileSize = GetSize(fileInfo.Length);
                    Desc = $"Size: {fileSize}{Desc}";
                }
                else if (fileSystemInfo is DirectoryInfo dirInfo)
                {
                    IsDirectory = true;
                    var ignore_rule_log_under_cache_on_cache = AppHelper.LogUnderCache && dirInfo.FullName == IOPath.CacheDirectory;
                    var filesCount = dirInfo.GetFiles().Length;
                    var dirsCount = dirInfo.GetDirectories().Length;
                    Desc = $"Count: {filesCount + (ignore_rule_log_under_cache_on_cache ? dirsCount - 1 : dirsCount)}{Desc}";
                }
                else
                {
                    throw new NotSupportedException();
                }
            }

            public bool IsDirectory { get; }

            public string Name { get; }

            public string FullName { get; }

            public string Desc { get; }
        }

        static IEnumerable<KeyValuePair<string, string>> GetRootPaths()
        {
            yield return new(IOPath.AppDataDirectory, IOPath.DirName_AppData);
            yield return new(AppHelper.LogDirPath, AppHelper.LogDirName);
            yield return new(IOPath.CacheDirectory, IOPath.DirName_Cache);
        }

        static T GetRootPathInfoViewModels<T>() where T : ICollection<PathInfoViewModel>, new()
        {
            return new T()
            {
                new PathInfoViewModel(new DirectoryInfo(IOPath.AppDataDirectory), IOPath.DirName_AppData),
                new PathInfoViewModel(new DirectoryInfo(IOPath.CacheDirectory), IOPath.DirName_Cache),
                new PathInfoViewModel(new DirectoryInfo(AppHelper.LogDirPath)),
            };
        }

        public static void AddRange(IList<PathInfoViewModel> list, string dirPath)
        {
            var ignore_rule_log_under_cache_on_cache = AppHelper.LogUnderCache && dirPath == IOPath.CacheDirectory;
            var dirInfo = new DirectoryInfo(dirPath);
            Array.ForEach(dirInfo.GetDirectories(), x =>
            {
                if (ignore_rule_log_under_cache_on_cache && x.Name == AppHelper.LogDirName) return;
                list.Add(new(x));
            });
            Array.ForEach(dirInfo.GetFiles(), x => list.Add(new(x)));
        }

        public void OnItemClick(PathInfoViewModel pathInfo)
        {
            if (pathInfo.IsDirectory)
            {
                CurrentPath = pathInfo.FullName;
            }
            else
            {
                var extension = Path.GetExtension(pathInfo.FullName);
                if (string.Equals(extension, FileEx.JSON, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, FileEx.TXT, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, FileEx.LOG, StringComparison.OrdinalIgnoreCase))
                {
                    IPlatformService.Instance.OpenFileByTextReader(pathInfo.FullName);
                }
            }
        }

        public bool OnBack()
        {
            if (Title != DefaultTitle)
            {
                var dirs = Title.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                if (dirs.Length == 1)
                {
                    CurrentPath = string.Empty;
                }
                else if (dirs.Length > 0)
                {
                    foreach (var item in GetRootPaths())
                    {
                        if (item.Value == dirs[0])
                        {
                            CurrentPath = Path.Combine(new[] { item.Key }.Concat(dirs.Skip(1).Take(dirs.Length - 2)).ToArray());
                            break;
                        }
                    }
                }
                else
                {
                    return false;
                }
                return true;
            }
            return false;
        }
    }
}