using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSearch.Model
{
    class Node
    {
        private List<Node> childNodes;

        /// <summary>
        /// 节点名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 节点完整名
        /// </summary>
        public string FullName { get; set; }

        /// <summary>
        /// 节点图标
        /// </summary>
        public object ImageSource { get; set; }

        /// <summary>
        /// 子节点集合
        /// </summary>
        public List<Node> ChildNodes
        {
            get
            {
                if (this.childNodes == null)
                    this.childNodes = new List<Node>();
                return this.childNodes;
            }
        }
    }
}
