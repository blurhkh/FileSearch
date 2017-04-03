using FileSearch.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Threading;

namespace FileSearch
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 根节点
        /// </summary>
        private List<Node> root;

        /// <summary>
        /// 根目录
        /// </summary>
        private string rootDirectory;

        /// <summary>
        /// 是否只显示文件夹
        /// </summary>
        private bool isOnlyFolder;

        /// <summary>
        /// 是否仅第一个
        /// </summary>
        private bool isOnlyFirst;

        /// <summary>
        /// 是否采用正则
        /// </summary>
        private bool isUseReg;

        /// <summary>
        /// 是否忽略大小写
        /// </summary>
        private bool isIgnoreCase;

        /// <summary>
        /// 检索条件
        /// </summary>
        private string searchCondition;

        /// <summary>
        /// 检索用正则
        /// </summary>
        private Regex regSearch;

        /// <summary>
        /// 是否结束当前检索
        /// </summary>
        private bool taskHasBeenFinished;

        /// <summary>
        /// 系统图标集合
        /// </summary>
        private List<AssociatedIcon> associatedIcons;

        /// <summary>
        /// 后台检索用线程
        /// </summary>
        private Thread thread;

        /// <summary>
        /// 文件完整路径缓存
        /// </summary>
        private Dictionary<string, string[]> fileCache;

        /// <summary>
        /// 文件夹完整路径缓存
        /// </summary>
        private Dictionary<string, string[]> folderCache;

        /// <summary>
        /// 驱动器
        /// </summary>
        private string[] driveNames;

        public MainWindow()
        {
            InitializeComponent();

            // 文本框聚焦
            this.txtSearchCondition.Focus();

            // 只初期化一次，下次可从缓存中读取
            this.associatedIcons = new List<AssociatedIcon>();

            this.fileCache = new Dictionary<string, string[]>();

            this.folderCache = new Dictionary<string, string[]>();

            this.thread = new Thread(() =>
            {
                MFTScanner mftScanner = new MFTScanner();
                this.driveNames
                       = DriveInfo.GetDrives()
                       .Where(x => x.DriveType == DriveType.Fixed)
                       .Select(x => x.Name).ToArray();
                foreach (string driveName in driveNames)
                {
                    string[] fileFullNames = mftScanner.EnumerateFiles(driveName).ToArray();
                    this.fileCache.Add(driveName, fileFullNames);
                    string[] folderFullNames = fileFullNames.Select(x => Path.GetDirectoryName(x)).Distinct().ToArray();
                    this.folderCache.Add(driveName, folderFullNames);
                }
                this.thread.Abort();
                this.thread = null;
            });
            this.thread.Start();
        }

        #region 事件
        /// <summary>
        /// 打开文件夹对话框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }
            this.txtDirectory.Text = folderBrowserDialog.SelectedPath;
        }

        /// <summary>
        /// 检索文本框回车
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtSearchCondition_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.btnSearch_Click(null, null);
            }
        }

        /// <summary>
        /// 检索按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            // 重置数据
            this.root = new List<Node>();
            this.rootDirectory = null;
            this.treeView.ItemsSource = null;

            this.txtSearchCondition.ToolTip = null;
            this.txtSearchCondition.Foreground = Brushes.Black;

            this.txtDirectory.ToolTip = "若未指定则全局搜索";
            this.txtDirectory.Foreground = Brushes.Black;

            this.taskHasBeenFinished = false;
            this.treeViewContextMenu.Visibility = Visibility.Collapsed;
            this.treeViewContextMenu.IsOpen = false;

            // 为避免跨线程访问
            this.isOnlyFolder = this.ckbOnlyFolder.IsChecked.Value;
            this.isOnlyFirst = this.ckbOnlyFirst.IsChecked.Value;
            this.isUseReg = this.ckbUseReg.IsChecked.Value;
            this.isIgnoreCase = this.ckbIgnoreCase.IsChecked.Value;
            this.searchCondition = this.isIgnoreCase ? this.txtSearchCondition.Text.ToLower() : this.txtSearchCondition.Text;

            // 检索条件为空则不进行检索
            if (string.IsNullOrEmpty(this.txtSearchCondition.Text.Trim())) return;

            bool errorFlg = false;

            // 检查正则表达式
            if (this.ckbUseReg.IsChecked.Value)
            {
                try
                {
                    if (this.ckbIgnoreCase.IsChecked.Value)
                    {
                        this.regSearch = new Regex(txtSearchCondition.Text, RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        this.regSearch = new Regex(txtSearchCondition.Text);
                    }
                }
                catch
                {
                    this.txtSearchCondition.Foreground = Brushes.Red;
                    this.txtSearchCondition.ToolTip = "请输入正确的表达式";
                    errorFlg = true;
                }
            }

            // 检查根目录
            if (!string.IsNullOrEmpty(this.txtDirectory.Text.Trim()))
            {
                if (!Directory.Exists(this.txtDirectory.Text))
                {
                    this.txtDirectory.Foreground = Brushes.Red;
                    this.txtDirectory.ToolTip = "请输入正确的目录";
                    errorFlg = true;
                }
            }

            // 无错误则继续
            if (errorFlg) return;

            this.StartSeach();
            this.rootDirectory = this.txtDirectory.Text.Trim();

            // 如果上一个线程尚未关闭
            if (this.thread != null)
            {
                this.thread.Abort();
                this.thread = null;
            }
            this.thread = new Thread(() =>
            {
                // 未指定根目录时
                if (string.IsNullOrEmpty(this.rootDirectory))
                {
                    foreach (string driveName in this.driveNames)
                    {
                        this.FindFile(driveName);
                        if (this.taskHasBeenFinished) break;
                    }
                }
                else
                {
                    // 统一改为加"\"
                    this.rootDirectory = this.rootDirectory + (this.rootDirectory.EndsWith("\\") ? null : "\\");
                    string driveName = System.IO.Path.GetPathRoot(this.rootDirectory);
                    this.FindFile(driveName);
                }

                // 跨线程访问
                this.Dispatcher.Invoke(() =>
            {
                this.treeView.ItemsSource = this.root;
                this.EndSeach();
            });
            });
            this.thread.Start();
        }

        /// <summary>
        /// 判断是否显示文件夹选择行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ckbFolder_Click(object sender, RoutedEventArgs e)
        {
            this.txtDirectory.Text = string.Empty;
            this.dockFolder.Visibility = this.ckbFolder.IsChecked.Value ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 取消查找
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.root = null;
            this.EndSeach();
        }

        /// <summary>
        /// 窗口关闭
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            this.EndSeach();
        }

        /// <summary>
        /// 文件树节点右键事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = this.VisualUpwardSearch(e.OriginalSource as DependencyObject) as TreeViewItem;
            if (treeViewItem != null)
            {
                this.treeViewContextMenu.Visibility = Visibility.Visible;
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 树结构右键
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeView = this.VisualUpwardSearch(e.OriginalSource as DependencyObject) as System.Windows.Controls.TreeView;
            if (treeView != null)
            {
                this.treeViewContextMenu.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 上下文菜单打开
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeViewContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (this.treeViewContextMenu.Visibility == Visibility.Collapsed)
            {
                // 隐藏状态不显示
                this.treeViewContextMenu.IsOpen = false;
            }
        }

        /// <summary>
        /// 打开所在文件夹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItemOpenContainingFolder_Click(object sender, RoutedEventArgs e)
        {
            string fullName = (this.treeView.SelectedItem as Node).FullName;
            System.Diagnostics.Process.Start("Explorer.exe", "/select," + fullName);
        }

        /// <summary>
        /// 打开文件夹或者文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void menuItemOpenFile_Click(object sender, RoutedEventArgs e)
        {
            string fullName = (this.treeView.SelectedItem as Node).FullName;
            System.Diagnostics.Process.Start(fullName);
        }
        #endregion

        #region 方法

        /// <summary>
        /// 查找文件
        /// </summary>
        private void FindFile(string driveName)
        {
            string[] fileFullNames = this.fileCache.Where(x => x.Key == driveName).FirstOrDefault().Value;
            // 如果一开始的后台线程没有加载完全，则继续加载
            if (fileFullNames == null)
            {
                // 因为如前一次执行没有调到Cleanup方法则会报错，所有重新实例化一个扫描器
                MFTScanner mftScanner = new MFTScanner();
                fileFullNames = mftScanner.EnumerateFiles(driveName).ToArray();
                this.fileCache.Add(driveName, fileFullNames);
            }
            if (!string.IsNullOrEmpty(this.rootDirectory))
            {
                // 指定根目录时进行过滤
                fileFullNames = fileFullNames.Where(x => x.StartsWith(this.rootDirectory)).ToArray();
            }
            string[] fullNames;
            if (this.isOnlyFolder)
            {
                // 只检索文件夹而不展开
                fullNames = this.folderCache.Where(x => x.Key == driveName).FirstOrDefault().Value;
                if (fullNames == null)
                {
                    fullNames = fileFullNames.Select(x => Path.GetDirectoryName(x)).Distinct().ToArray();
                    this.folderCache.Add(driveName, fullNames);
                }
            }
            else
            {
                fullNames = fileFullNames;
            }
            foreach (var fullName in fullNames)
            {
                string[] fileNameOrFolderNames = fullName.Split('\\').Skip(1).ToArray();
                foreach (string name in fileNameOrFolderNames)
                {
                    if (this.IsMatch(name))
                    {
                        string fullNameNew = fullName;
                        if (this.isOnlyFolder)
                        {
                            // 只检索文件夹而不展开内部文件夹
                            fullNameNew = Regex.Replace(fullName, $@"(?<={name}[^\\]*)\\.*", "");
                        }
                        string[] nodeNames;
                        if (string.IsNullOrEmpty(this.rootDirectory))
                        {
                            nodeNames = fullNameNew.Split('\\');
                        }
                        else
                        {
                            nodeNames = fullNameNew.Replace(this.rootDirectory, "").Split('\\');
                        }
                        this.CreatNodes(nodeNames, this.root, this.rootDirectory);
                        // 若只查找一个文件则结束查询 
                        if (this.isOnlyFirst)
                        {
                            this.taskHasBeenFinished = true;
                            return;
                        };
                    }
                }
            }
        }

        /// <summary>
        /// 判断是否所查询的文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private bool IsMatch(string fileName)
        {
            if (this.isUseReg)
            {
                return this.regSearch.IsMatch(fileName);
            }
            else
            {
                if (this.isIgnoreCase)
                {
                    return fileName.ToLower().Contains(this.searchCondition);
                }
                else
                {
                    return fileName.Contains(this.searchCondition);
                }
            }
        }

        /// <summary>
        /// 生成节点
        /// </summary>
        /// <param name="nodeNames">节点名数组</param>
        /// <param name="Nodes">当前层次的节点集合</param> 
        /// <param name="fileFullName">文件完整路径</param>
        /// <param name="parentFullName">父节点完整路径</param>
        private void CreatNodes(string[] nodeNames, List<Node> nodes, string parentFullName)
        {
            Node node = nodes.Where(x => x.Name == nodeNames[0]).FirstOrDefault();
            if (node == null)
            {
                // 若不存在则创建新节点
                node = new Node
                {
                    Name = nodeNames[0],
                    FullName = String.IsNullOrEmpty(parentFullName) ? nodeNames[0] :
                    (parentFullName + (parentFullName.EndsWith("\\") ? null : "\\") + nodeNames[0])
                };
                nodes.Add(node);
            }

            if (nodeNames.Length > 1)
            {
                // 文件夹则继续创建子节点
                node.ImageSource = "/Resources/Ico/folder.ico";
                this.CreatNodes(nodeNames.Skip(1).ToArray(), node.ChildNodes, node.FullName);
            }
            else if (this.isOnlyFolder)
            {
                node.ImageSource = "/Resources/Ico/folder.ico";
            }
            else
            {
                // 文件
                node.ImageSource = this.GetAssociatedIcon(node.FullName);
            }
        }

        /// <summary>
        /// 获取系统图标
        /// </summary>
        /// <param name="filePath">文件全路径</param>
        private BitmapImage GetAssociatedIcon(string filePath)
        {
            BitmapImage bitmapImage;
            string extension = System.IO.Path.GetExtension(filePath);
            AssociatedIcon associatedIcon = this.associatedIcons.Where(x => x.Extension == extension).FirstOrDefault();
            if (associatedIcon != null)
            {
                // 如果已经缓存过，则不再再次读取
                bitmapImage = associatedIcon.ImageSource;
            }
            else
            {
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
                    this.associatedIcons.Add(new AssociatedIcon
                    {
                        Extension = extension,
                        ImageSource = bitmapImage
                    });
                }
                catch
                {
                    // 有的图标可能无法获取
                    return null;
                }
            }
            return bitmapImage;
        }

        /// <summary>
        /// 开始检索
        /// </summary>
        private void StartSeach()
        {
            this.gifLoading.StartAnimate();
            this.btnSearch.Visibility = Visibility.Collapsed;
            this.btnCancel.Visibility = Visibility.Visible;
            this.treeViewContextMenu.Visibility = Visibility.Collapsed;
            this.treeViewContextMenu.IsOpen = false;
        }

        /// <summary>
        /// 结束检索
        /// </summary>
        private void EndSeach()
        {
            this.gifLoading.StopAnimate();
            this.btnCancel.Visibility = Visibility.Collapsed;
            this.btnSearch.Visibility = Visibility.Visible;

            // 有节点时才显示右键菜单
            if (this.root != null && this.root.Count() > 0)
            {
                this.treeViewContextMenu.Visibility = Visibility.Visible;
            }
            // 销毁后台线程
            if (this.thread != null)
            {
                this.thread.Abort();
                this.thread = null;
            }
        }
        /// <summary>
        /// 节点查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        private DependencyObject VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem || source is System.Windows.Controls.TreeView))
            {
                source = VisualTreeHelper.GetParent(source);
            }
            return source;
        }
        #endregion      
    }
}
