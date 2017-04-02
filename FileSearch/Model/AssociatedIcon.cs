using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace FileSearch.Model
{
    public class AssociatedIcon
    {
        /// <summary>
        /// 文件后缀
        /// </summary>
        public string Extension { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public BitmapImage ImageSource { get; set; }
    }
}
