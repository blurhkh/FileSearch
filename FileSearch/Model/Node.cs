using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FileSearch.Model
{
    class Node
    {
        private ObservableCollection<Node> childNodes;

        /// <summary>
        /// 节点名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 节点完整名
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// 是否是文件
        /// </summary>
        public bool IsFile { get; set; }

        /// <summary>
        /// 子节点集合
        /// </summary>
        public ObservableCollection<Node> ChildNodes
        {
            get
            {
                if (this.childNodes == null)
                    this.childNodes = new ObservableCollection<Node>();
                return this.childNodes;
            }
        }

        /// <summary>
        /// 节点图标
        /// </summary>
        public object ImageSource
        {
            get
            {
                if (this.IsFile)
                {
                    return this.GetAssociatedIcon(this.FullName);
                }
                else
                {
                    return "/Resources/Ico/folder.ico";
                }
            }
        }

        /// <summary>
        /// 获取系统图标
        /// </summary>
        /// <param name="filePath">文件全路径</param>
        private BitmapImage GetAssociatedIcon(string filePath)
        {
            BitmapImage bitmapImage = null;
            try
            {
                // 此处不能释放该memoryStream，否则bitmapImage数据将消失
                MemoryStream memoryStream = new MemoryStream();
                System.Drawing.Bitmap bitmap = System.Drawing.Icon.ExtractAssociatedIcon(filePath).ToBitmap();
                bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            catch
            {
                // 有的图标可能无法获取
            }
            return bitmapImage;
        }
    }
}
