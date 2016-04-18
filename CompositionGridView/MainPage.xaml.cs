using System;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.BulkAccess;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;

namespace CompositionGridViewApp
{
    public sealed partial class MainPage : Page
    {
        private StorageFolder _currentFolder = null;
        private ObservableCollection<BogusFileItem> _fileItemSource = new ObservableCollection<BogusFileItem>();

        // ---- UI.Composition その０ ----
        private Compositor _compositor = null;
        private int nAnimationDulation = 300;
        const int GRIDITEM_ANIMATION_DELAYOFFSET = 0;


        public MainPage()
        {
            this.InitializeComponent();

            // ---- UI.Composition その１ -----
            // Compositorを取得 ここではこのPageそのもののCompositorを使う
            // ElementCompositionPreview でXAMLのElementからVisualLayerに繋げるCompositorを取得する
            _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // ファイルリストの取得 今回ここは主題では無いので省略
            _currentFolder = KnownFolders.PicturesLibrary;
            await loadFileItemsToGridViewAsync();
        }

        /// <summary>
        /// ---- UI.Composition その２ -----
        /// ListView系でのUI.Compositionのお絵かきはCCCからやるのが定石らしい（Sessionでそんな事を言っていた）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void itemGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            // Recycle中でなければ、ItemContainerのロードでUI.Compositionの作業を行う
            if (!args.InRecycleQueue)
            {
                args.ItemContainer.Loaded += ItemContainer_Loaded;
            }
        }

        /// <summary>
        /// ---- UI.Composition その３ -----
        /// UI.Compositionの作業を行うメイン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ItemContainer_Loaded(object sender, RoutedEventArgs e)
        {
            // 一回だけでいいのでイベント外す
            var itemContainer = sender as SelectorItem;
            itemContainer.Loaded -= ItemContainer_Loaded;
            
            var itemsPanel = (ItemsWrapGrid)itemGridView.ItemsPanelRoot;
            var itemIndex = itemGridView.IndexFromContainer(itemContainer);

            var gd = itemContainer.ContentTemplateRoot as Grid;

            // 見えてるアイテム限定　このため、アニメ中にスクロールがーっとすると見えてる所からアニメするのが見られる
            if (/*-1 != itemIndex && */itemIndex >= itemsPanel.FirstVisibleIndex && itemIndex <= itemsPanel.LastVisibleIndex)
            {
                // 定石 ElementCompositionPreview.GetElementVisual で これから作業するUIElementの「Visual」を取得
                // ここではitemContainer...GridViewItemのコンテナ、を単位として、そいつの描画をいじくることになる
                var itemVisual = ElementCompositionPreview.GetElementVisual(itemContainer);

                // Visualの初期値設定
                float width = (float)gd.RenderSize.Width;
                float height = (float)gd.RenderSize.Height;
                itemVisual.Size = new Vector2(width, height);
                itemVisual.CenterPoint = new Vector3(width / 2, height / 2, 0f);

                // アニメで使うイージングの設定　ベジェ曲線でイージングのカーブを設定するタイプ CSSとかと同じ
                // CSSのベジェ設定データをそのまま使い回せる

                // 加速度付けた動きがカッコイイ（と思う）のでそういうのを設定
                var qubicEaseIn = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.55f, 0.555f), new Vector2(0.675f, 0.19f));
                var qubicEaseOut = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.215f, 0.61f), new Vector2(0.355f, 1f));

                // ここからアニメーション4種類を別々に設定
                // StartAnimationはここを抜けたら一斉に（同時に）始まるので　ここでの書き順は特に影響がない

                // １　オフセット ここでは左から右にアイテムを動かす
                KeyFrameAnimation offsetAnimation = _compositor.CreateScalarKeyFrameAnimation();
                offsetAnimation.InsertExpressionKeyFrame(1f, "0", qubicEaseOut);
                offsetAnimation.Duration = TimeSpan.FromMilliseconds(nAnimationDulation);
                // ItemIndex毎にアニメーションのスタートを少しづつ遅らせて順にびろろろろーんという効果を出す
                offsetAnimation.DelayTime = TimeSpan.FromMilliseconds(itemIndex * GRIDITEM_ANIMATION_DELAYOFFSET);
                // 初期値とStartAnimation
                itemVisual.Offset = new Vector3(-100, 0, 0);
                // 第一パラメータで渡しているのはこのitemVisualで動かすプロパティ名
                itemVisual.StartAnimation("Offset.X", offsetAnimation);

                // ２　回転 ここではZ軸でくるっと回す
                KeyFrameAnimation rotAnimation = _compositor.CreateScalarKeyFrameAnimation();
                // KeyFrame "1"がアニメ終了地点
                rotAnimation.InsertExpressionKeyFrame(1f, "0", qubicEaseOut);
                rotAnimation.Duration = TimeSpan.FromMilliseconds(nAnimationDulation);
                rotAnimation.DelayTime = TimeSpan.FromMilliseconds(itemIndex * GRIDITEM_ANIMATION_DELAYOFFSET);
                //　Z軸に-90回転,を初期値とする
                itemVisual.RotationAxis = new Vector3(0, 0f, 1f);
                itemVisual.RotationAngleInDegrees = -90;
                itemVisual.StartAnimation("RotationAngleInDegrees", rotAnimation);

                // ３　スケール ここでは3倍から1倍に縮小する
                // ちなみに、1倍以下から拡大する場合描画が微妙に変…な場合がある
                Vector3KeyFrameAnimation scaleAnimation = _compositor.CreateVector3KeyFrameAnimation();
                //scaleAnimation.InsertKeyFrame(0, new Vector3(1f, 1f, 0f));
                scaleAnimation.InsertKeyFrame(0, new Vector3(3f, 3f, 0f));
                //scaleAnimation.InsertKeyFrame(0.1f, new Vector3(0.05f, 0.05f, 0.05f));
                scaleAnimation.InsertKeyFrame(1f, new Vector3(1f, 1f, 0f), qubicEaseIn);
                scaleAnimation.Duration = TimeSpan.FromMilliseconds(nAnimationDulation);
                scaleAnimation.DelayTime = TimeSpan.FromMilliseconds(itemIndex * GRIDITEM_ANIMATION_DELAYOFFSET);
                itemVisual.Scale = new Vector3(1, 1, 1);
                itemVisual.StartAnimation("Scale", scaleAnimation);

                // ４　フェード 透明度を0から1へ変化させる
                KeyFrameAnimation fadeAnimation = _compositor.CreateScalarKeyFrameAnimation();
                fadeAnimation.InsertExpressionKeyFrame(1f, "1", qubicEaseIn);
                fadeAnimation.Duration = TimeSpan.FromMilliseconds(nAnimationDulation);
                fadeAnimation.DelayTime = TimeSpan.FromMilliseconds(itemIndex * GRIDITEM_ANIMATION_DELAYOFFSET);
                itemVisual.Opacity = 0f;
                itemVisual.StartAnimation("Opacity", fadeAnimation);
            }
        }

        /// <summary>
        /// 今のフォルダでリロード
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void btnReload_Click(object sender, RoutedEventArgs e)
        {
            await loadFileItemsToGridViewAsync();
        }

        /// <summary>
        /// 現在のフォルダからファイルを100個まで拾ってUIにはめる
        /// </summary>
        /// <returns></returns>
        private async Task<bool> loadFileItemsToGridViewAsync()
        {
            bool retVal = false;

            //tbFolderName.Text = _currentFolder.DisplayName;
            btnGo.Content = "Reload " + _currentFolder.DisplayName;

            itemGridView.ItemsSource = null;
            _fileItemSource.Clear();

            var items = await _currentFolder.GetItemsAsync();

            itemGridView.ItemsSource = _fileItemSource;

            int nNum = 0;

            foreach (var item in items)
            {
                if( 100 < nNum) { break; }

                var fileitem = item as StorageFile;
                if (null != fileitem)
                {
                    var bfi = new BogusFileItem(fileitem);
                    await bfi.PrepareThumbnailAsync();

                    _fileItemSource.Add(bfi);
                    nNum++;
                }
            }

            return retVal;
        }


        private async void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker fop = new FolderPicker();
            fop.ViewMode = PickerViewMode.Thumbnail;
            fop.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fop.FileTypeFilter.Add(".jpg");
            fop.FileTypeFilter.Add(".jpeg");
            fop.FileTypeFilter.Add(".png");
            fop.FileTypeFilter.Add(".gif");
            fop.SettingsIdentifier = "selectSaveFolder";

            StorageFolder folder = await fop.PickSingleFolderAsync();

            if (null != folder)
            {
                this._currentFolder = folder;
                await loadFileItemsToGridViewAsync();
            }
        }

        private void sliderDuration_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            nAnimationDulation = (int)e.NewValue;
        }
    }

    /// <summary>
    /// GetVirtualizedItemsVectorはThumbnailにImagesourceを突っ込んでくるので、それを表示できるImageSoureceに変換してあげるコンバーター
    /// </summary>
    public sealed class ImageSourceToImageConverter : Windows.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string culture)
        {
            if ((null == value) /*|| (!typeof(ImageSource).Equals(value.GetType()))*/ )
                return null;

            var thumbnailStream = (IRandomAccessStream)value;
            var bi = new BitmapImage();

            bi.SetSource(thumbnailStream);

            return bi;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string culture)
        {
            return true;
        }
    }

    public class BogusFileItem
    {
        private StorageFile _file;

        public BogusFileItem()
        {

        }

        public BogusFileItem(StorageFile file)
        {
            _file = file;
            this.Name = _file.Name;
        }

        public async Task<bool> PrepareThumbnailAsync()
        {
            bool retVal = false;

            this.thumbnail = await _file.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.PicturesView, 190);
            
            return retVal;
        }

        private IRandomAccessStream thumbnail = null;
        public IRandomAccessStream Thumbnail {
            get
            {
                return this.thumbnail;
            }
            set
            {
            }
        }

        public string Name { get; set; }

    }
}
