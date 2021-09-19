using System.Threading.Tasks;
using askfmArchiver.Enums;

namespace askfmArchiver.Utils
{
    public interface IFileManager
    {
        bool CheckDir(string dir);
        string ComputeHash(string file);
        Task SaveData<T>(T data, string file, FileType type);
    }
}