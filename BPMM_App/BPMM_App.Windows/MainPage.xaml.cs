﻿using BPMM_App.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.Popups;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Data.Json;
using Windows.Storage.Streams;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Windows.UI.Text;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace BPMM_App
{

    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {      
        private NavigationHelper navigationHelper;
        private ObservableDictionary defaultViewModel = new ObservableDictionary();

        private string user = "Sebastian";
        private string password;

        public static List<BaseControl> controls = new List<BaseControl>();
        private static List<Link> links = new List<Link>();

        private bool linking = false;
        private Link currentLine;
        private BaseControl sourceControl;
        
        private bool selecting;
        private Point selectionStartPoint;
        private Rectangle selectionBox;

        private bool dragging; 

        double warningsPaneSize = 100;
        private ObservableCollection<WarningItem> warnings = new ObservableCollection<WarningItem>();
        public event PropertyChangedEventHandler PropertyChanged;

        public MainPage()
        {
            this.InitializeComponent();
            this.navigationHelper = new NavigationHelper(this);
            this.navigationHelper.LoadState += navigationHelper_LoadState;
            this.navigationHelper.SaveState += navigationHelper_SaveState;
            DataContext = this;
        }

        public ObservableDictionary DefaultViewModel
        {
            get { return defaultViewModel; }
        }

        public NavigationHelper NavigationHelper
        {
            get { return navigationHelper; }
        }

        public ObservableCollection<WarningItem> Warnings
        {
            get { return warnings; }
            set { warnings = value; OnPropertyChanged("Warnings"); }
        }

        public void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }


        private void navigationHelper_LoadState(object sender, LoadStateEventArgs e)
        {
            // restore session data
            if (e.PageState != null && e.PageState.ContainsKey("key"))
            {

            }
            // restore app data
            //ApplicationDataContainer roaming = ApplicationData.Current.RoamingSettings;
            //if (roaming.Containers.ContainsKey("user"))
            //{
            //    var username = (roaming.Containers["user"].Values.ContainsKey("name")) ? (string)roaming.Containers["user"].Values["name"] : "";
            //    var passwd = (roaming.Containers["user"].Values.ContainsKey("password")) ? (string)roaming.Containers["user"].Values["name"] : "";
            //}
        }

        private void navigationHelper_SaveState(object sender, SaveStateEventArgs e)
        {
            //ApplicationDataContainer roaming = ApplicationData.Current.RoamingSettings;
            //var container = roaming.CreateContainer("user", ApplicationDataCreateDisposition.Always);
            //if (roaming.Containers.ContainsKey("user"))
            //{
            //    roaming.Containers["user"].Values["name"] = user;
            //    var buffer = CryptographicBuffer.ConvertStringToBinary(password, BinaryStringEncoding.Utf8);
            //    var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
            //    var hash = hasher.HashData(buffer);
            //    roaming.Containers["user"].Values["password"] = CryptographicBuffer.EncodeToBase64String(hash);
            //}
        }

        #region NavigationHelper-Registrierung
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            navigationHelper.OnNavigatedFrom(e);
        }
        #endregion

        private void StackPanel_Drop(object sender, DragEventArgs e)
        {
            object item;
            if (e.Data.Properties.TryGetValue("Item", out item))
            {
                Category category = (Category)item;
                BaseControl control =
                            (category == Category.NOTE) ? (BaseControl)new NoteControl() :
                            (category == Category.ASSESSMENT) ? (BaseControl)new AssessmentControl() :
                            (category == Category.BUSINESS_RULE) ? (BaseControl)new BusinessRuleControl() :
                            (category == Category.INFLUENCER) ? (BaseControl)new InfluencerControl() :
                            (BaseControl)new BPMMControl(category);

                control.LinkStartEvent += OnLinkStart;
                control.LinkEndEvent += OnLinkEnd;
                control.DeleteEvent += DeleteControl;
                control.MovedEvent += ControlMoved;
                control.MoveEndEvent += ControlStoppedMoving;
                controls.Add(control);
                if (control is BPMMControl)
                {
                    BPMMControl bpmm = (BPMMControl)control;
                    bpmm.WarningsAddedEvent += AddWarnings;
                    bpmm.WarningsRemovedEvent += RemoveWarnings;


                    foreach (var warnItem in bpmm.getWarnings())
                    {
                        Warnings.Add(warnItem);
                    }
                }
                Point pos = e.GetPosition(workspace);
                Canvas.SetLeft(control, pos.X);
                Canvas.SetTop(control, pos.Y);
                workspace.Children.Add(control);
                if (step == TourStep.V1 && category == Category.VISION
                    || step == TourStep.G1 && category == Category.GOAL
                    || step == TourStep.O1 && category == Category.OBJECTIVE
                    || step == TourStep.S1 && category == Category.STRATEGY
                    || step == TourStep.T1 && category == Category.TACTIC
                    || step == TourStep.I1 && category == Category.INFLUENCER
                    || step == TourStep.A1 && category == Category.ASSESSMENT
                    || step == TourStep.P1 && category == Category.BUSINESS_POLICY
                    || step == TourStep.R1 && category == Category.BUSINESS_RULE)
                {
                    performStep(step + 1);
                }
            }
        }

        public static BaseControl getControl(int id)
        {
            foreach (var control in controls)
            {
                if (control.id == id)
                {
                    return control;
                }
            }
            return null;
        }

        private double maxX()
        {
            double maxX = 0;
            foreach (var control in controls)
            {
                control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxX = Math.Max(maxX, Canvas.GetLeft(control) + control.ActualWidth);
            }
            return maxX;
        }

        private double minX()
        {
            double minX = double.PositiveInfinity;
            foreach (var control in controls)
            {
                minX = Math.Min(minX, Canvas.GetLeft(control));
            }
            return minX;
        }

        private double maxY()
        {
            double maxY = 0;
            foreach (var control in controls)
            {
                control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                maxY = Math.Max(maxY, Canvas.GetTop(control) + control.ActualHeight);
            }
            return maxY;
        }

        private double minY()
        {
            double minY = double.PositiveInfinity;
            foreach (var control in controls)
            {
                minY = Math.Min(minY, Canvas.GetTop(control));
            }
            return minY;
        }

        private void ListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items[0].Equals(visionIcon)) { e.Data.Properties.Add("Item", Category.VISION); }
            else if (e.Items[0].Equals(goalIcon)) { e.Data.Properties.Add("Item", Category.GOAL); }
            else if (e.Items[0].Equals(objectiveIcon)) { e.Data.Properties.Add("Item", Category.OBJECTIVE); }
            else if (e.Items[0].Equals(missionIcon)) { e.Data.Properties.Add("Item", Category.MISSION); }
            else if (e.Items[0].Equals(strategyIcon)) { e.Data.Properties.Add("Item", Category.STRATEGY); }
            else if (e.Items[0].Equals(tacticIcon)) { e.Data.Properties.Add("Item", Category.TACTIC); }
            else if (e.Items[0].Equals(policyIcon)) { e.Data.Properties.Add("Item", Category.BUSINESS_POLICY); }
            else if (e.Items[0].Equals(ruleIcon)) { e.Data.Properties.Add("Item", Category.BUSINESS_RULE); }
            else if (e.Items[0].Equals(influencerIcon)) { e.Data.Properties.Add("Item", Category.INFLUENCER); }
            else if (e.Items[0].Equals(assessmentIcon)) { e.Data.Properties.Add("Item", Category.ASSESSMENT); }
            else if (e.Items[0].Equals(note)) { e.Data.Properties.Add("Item", Category.NOTE); }
        }
        #region link drawing
        public void OnLinkStart(object sender, PointerRoutedEventArgs e)
        {
            sourceControl = (BaseControl)sender;
            Point p = e.GetCurrentPoint(workspace).Position;
            currentLine = new Link(sourceControl, p, p);
            sourceControl.MovedEvent += currentLine.sourceMoved;
            currentLine.DeleteEvent += DeleteLink;
            links.Add(currentLine);
            workspace.Children.Add(currentLine);
            linking = true;
        }

        public void OnLinkEnd(object sender, EventArgs e)
        {
            if (currentLine == null)
            { // case: simple click on control
                return;
            }

            BaseControl target = (BaseControl)sender;

            if (target == sourceControl)
            { // case: link to itself
                Debug.WriteLine("Cannot pull link to itself.");
                workspace.Children.Remove(currentLine);
                links.Remove(currentLine);
                return;
            }
            linkControls(sourceControl, target);
            target.MovedEvent += currentLine.targetMoved;
            sourceControl.anchor.Opacity = 0.4;
            if (sourceControl is BPMMControl && target is BPMMControl)
            {
                ((BPMMControl)target).WarningsAddedEvent += AddWarnings;
                ((BPMMControl)target).WarningsRemovedEvent += RemoveWarnings;
                ((BPMMControl)sourceControl).validateNewLink(((BPMMControl)target).category);
                ((BPMMControl)target).validateNewLink(((BPMMControl)sourceControl).category);
            }
            sourceControl = null;
            currentLine = (step == TourStep.None)? null : currentLine;
            linking = false;
        }

        private void linkControls(BaseControl source, BaseControl target)
        {
            source.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            target.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var s = new Point(Canvas.GetLeft(source), Canvas.GetTop(source));
            var s2 = new Point(s.X + source.getWidth(), s.Y + source.getHeight()) ;
            var t = new Point(Canvas.GetLeft(target), Canvas.GetTop(target));
            var t2 = new Point(t.X + target.getWidth(), t.Y + target.getHeight());

            if (s2.X < t.X)
            { // case: target is right from source
                currentLine.updateStartPoint(source, new Point(s.X + source.getWidth(), s.Y + source.getHeight() / 2));
                var targetPoint =
                    (s.Y > t2.Y) ? new Point(t.X + target.getWidth() / 2, t2.Y) : // target right and above of source
                    (s2.Y < t.Y) ? new Point(t.X + target.getWidth() / 2, t.Y) : // target right and below source
                    new Point(t.X, t.Y + target.getHeight() / 2); // target right of source
                currentLine.updateEndPoint(target, targetPoint);
                    
            }
            else if (s.X > t2.X)
            { // case: target is left from source
                currentLine.updateStartPoint(source, new Point(s.X, s.Y + source.getHeight() / 2));
                var targetPoint =
                    (s.Y > t2.Y) ? new Point(t.X + target.getWidth() / 2, t2.Y) : // target left and above of source
                    (s2.Y < t.Y) ? new Point(t.X + target.getWidth() / 2, t.Y) : // target left and below of source
                    new Point(t2.X, t.Y + target.getHeight() / 2); // target left of source
                currentLine.updateEndPoint(target, targetPoint);
            }
            else if (s.Y > t2.Y)
            { // case: target is neither left nor right of source, but above of it
                currentLine.updateStartPoint(source, new Point(s.X + source.getWidth() / 2, s.Y));
                currentLine.updateEndPoint(target, new Point(t.X + target.getWidth() / 2, t2.Y));
            }
            else
            { // case: target is neither left nor right of source, but below it 
                currentLine.updateStartPoint(source, new Point(s.X + source.getWidth() / 2, s2.Y));
                currentLine.updateEndPoint(target, new Point(t.X + target.getWidth() / 2, t.Y));
            }
        }
        #endregion

        private void workspace_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (linking)
            {
                if (e.IsInertial)
                {
                    e.Handled = true;
                    return;
                }
                Point target = e.Position;
                target.X -= 5;
                target.Y -= 5;
                currentLine.updateEndPoint(null, target);
            }
            else if (e.Delta.Scale != 0)
            {
                foreach (var control in controls)
                {

                    control.Resize(e.Delta.Scale);
                    control.UpdateFontSize(e.Delta.Scale);
                }
                foreach (var link in links)
                {
                    link.UpdateFontSize(e.Delta.Scale);
                }
                workspace.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                workspace.Width = Math.Max(maxX(), workspace.ActualWidth);
                workspace.Height = Math.Max(maxY(), workspace.ActualHeight);
            }
            else if(dragging)
            {
                workspaceScroll.ChangeView(workspaceScroll.HorizontalOffset + e.Delta.Translation.X,
                    workspaceScroll.VerticalOffset + e.Delta.Translation.Y, 1);
            }
        }

        private void workspace_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (linking)
            {
                linking = false;
                workspace.Children.Remove(currentLine);
                links.Remove(currentLine);
                currentLine = null;
                return;
            }
            dragging = true;
            workspace.CapturePointer(e.Pointer);
            //selecting = true;
            //selectionStartPoint = e.GetCurrentPoint(workspace).Position;
            //selectionBox = new Rectangle()
            //{
            //    Width = Math.Abs(selectionStartPoint.X - selectionStartPoint.X),
            //    Height = Math.Abs(selectionStartPoint.Y - selectionStartPoint.Y),
            //    Fill = new SolidColorBrush(Colors.Blue),
            //    Stroke = new SolidColorBrush(Colors.Blue) { Opacity = 1 },
            //    StrokeThickness = 4,
            //    Opacity = 0.2
            //};
            //Canvas.SetLeft(selectionBox, selectionStartPoint.X);
            //Canvas.SetTop(selectionBox, selectionStartPoint.Y);
            //workspace.Children.Add(selectionBox);
            //workspace.CapturePointer(e.Pointer);
        }

        private void workspace_PointerMoved(object sender, PointerRoutedEventArgs e)
        {


            //else if (selecting)
            //{
            //    Point currPoint = e.GetCurrentPoint(workspace).Position;
            //    currPoint.X = (currPoint.X > workspace.Width) ? workspace.Width
            //                                                    : (currPoint.X < 0) ? 0 : currPoint.X;
            //    currPoint.Y = (currPoint.Y > workspace.Height) ? workspace.Height
            //                                                    : (currPoint.Y < 0) ? 0 : currPoint.Y;
            //    if (currPoint.X < selectionStartPoint.X)
            //    {
            //        Canvas.SetLeft(selectionBox, currPoint.X);
            //    }
            //    if (currPoint.Y < selectionStartPoint.Y)
            //    {
            //        Canvas.SetTop(selectionBox, currPoint.Y);
            //    }
            //    selectionBox.Width = Math.Abs(currPoint.X - selectionStartPoint.X);
            //    selectionBox.Height = Math.Abs(currPoint.Y - selectionStartPoint.Y);
            //}
        }

        private void workspace_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (linking)
            {
                linking = false;
                workspace.Children.Remove(currentLine);
                links.Remove(currentLine);
                currentLine = null;
            }

            //else if (selecting)
            //{
            //    Point currPoint = e.GetCurrentPoint(workspace).Position;
            //    selectionBox.Width = Math.Abs(currPoint.X - selectionStartPoint.X);
            //    selectionBox.Height = Math.Abs(currPoint.Y - selectionStartPoint.Y);
            //    workspace.Children.Remove(selectionBox);
            //    selecting = false;
            //    workspace.ReleasePointerCapture(e.Pointer);
            //}
        }
        
        public void DeleteControl(object sender, EventArgs e)
        {
            foreach (var link in findLinks((BaseControl)sender))
            {
                DeleteLink(link, e);
            }
            if(sender is BPMMControl)
            {
                RemoveWarnings(sender, ((BPMMControl)sender).warnings);
            }
            workspace.Children.Remove((BaseControl)sender);
            controls.Remove((BaseControl)sender);
        }

        public void DeleteLink(object linkObj, EventArgs e)
        {
            var link = (Link)linkObj;
            workspace.Children.Remove(link);
            links.Remove(link);

            if (link.sourceControl is BPMMControl && link.targetControl is BPMMControl)
            {
                var source = (BPMMControl)link.sourceControl;
                var target = (BPMMControl)link.targetControl;
                source.validateRemovedLink(target.category, (findLinks(source)).FindAll(x => x.sourceControl is BPMMControl && x.targetControl is BPMMControl));
                target.validateRemovedLink(source.category, (findLinks(target)).FindAll(x => x.sourceControl is BPMMControl && x.targetControl is BPMMControl));
            }
        }

        private void ControlMoved(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var control = (BaseControl)sender;
            Point p = new Point(Canvas.GetLeft(control), Canvas.GetTop(control));
            control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            workspace.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            if (p.X < 0)
            {
                workspace.Width = workspace.ActualWidth + Math.Abs(p.X);
                workspace.UpdateLayout();
                Canvas.SetLeft(control, 0);
            }
            else if (p.X + control.DesiredSize.Width > workspace.ActualWidth)
            {
                workspace.Width = p.X + control.DesiredSize.Width;
                workspaceScroll.ChangeView(p.X + control.DesiredSize.Width, null, null);
            }
            if (p.Y < 0)
            {
                workspace.Height = workspace.DesiredSize.Height + Math.Abs(p.Y);
                workspace.UpdateLayout();
                Canvas.SetTop(control, 0);
            }
            else if (p.Y + control.DesiredSize.Height > workspace.ActualHeight)
            {
                workspace.Height = p.Y + control.DesiredSize.Height;
                workspaceScroll.ChangeView(null, p.Y + control.DesiredSize.Height, null);
            }
        }
        private void ControlStoppedMoving(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            var control = (BaseControl)sender;
            control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double xBound = maxX();
            double yBound = maxY();
            workspace.Width = Math.Max(xBound, workspaceScroll.ActualWidth);
            workspace.Height = Math.Max(yBound, workspaceScroll.ActualHeight);
            workspaceScroll.ChangeView(Canvas.GetLeft(control), Canvas.GetTop(control), null);
        }

        private string serialize()
        {
            var root = new JsonObject();
            var controlArray = new JsonArray();
            var linkArray = new JsonArray();
            foreach (var child in workspace.Children)
            {
                if (child is BaseControl)
                {
                    controlArray.Add(((BaseControl)child).serialize());
                }
                else if (child is Link)
                {
                    linkArray.Add(((Link)child).serialize());
                }
            }
            root.Add("controls", controlArray);
            root.Add("links", linkArray);
            Debug.WriteLine(root.Stringify());
            return root.Stringify();
        }

        private bool deserialize(string input)
        {
            JsonObject data;
            if (JsonObject.TryParse(input, out data) == false)
            {
                return false;
            }

            var controlArray = data.GetNamedArray("controls", null);
            foreach(var entry in controlArray)
            {
                var value = entry.GetObject().GetNamedNumber("category", -1);
                if (value == -1)
                {
                    return false;
                }
                var type = (Category)value;
                BaseControl control =
                    (type == Category.NOTE) ? (BaseControl)NoteControl.deserialize(entry.GetObject()) :
                    (type == Category.BUSINESS_RULE) ? BusinessRuleControl.deserialize(entry.GetObject()) :
                    (type == Category.INFLUENCER) ? InfluencerControl.deserialize(entry.GetObject()) :
                    (type == Category.ASSESSMENT) ? AssessmentControl.deserialize(entry.GetObject()) :
                    BPMMControl.deserialize(entry.GetObject());
                if (control != null)
                {
                    workspace.Children.Add(control);
                    controls.Add(control);
                }
            }
            var linkArray = data.GetNamedArray("links", null);
            foreach (var entry in linkArray)
            {
                var link = Link.deserialize(entry.GetObject());
                if (link != null)
                {
                    workspace.Children.Add(link);
                    link.sourceControl.MovedEvent += link.sourceMoved;
                    link.targetControl.MovedEvent += link.targetMoved;
                }
            }
            return true;
        }

        private async void Save_Pressed(object sender, PointerRoutedEventArgs e)
        {
            var fileSaver = new FileSavePicker();
            fileSaver.FileTypeChoices.Add("BPMM", new List<String>{ ".json" });
            var file = await fileSaver.PickSaveFileAsync();
            if (file != null)
            {
                
                using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    fileStream.Size = 0;
                    using (var outputStream = fileStream.GetOutputStreamAt(0))
                    {
                        using (var dataWriter = new DataWriter(outputStream))
                        {
                            dataWriter.WriteString(serialize());
                            await dataWriter.StoreAsync();
                            dataWriter.DetachStream();
                        }
                        await outputStream.FlushAsync();
                    }
                }
                MessageDialog infoPopup = new MessageDialog(String.Format("Saved to file {0}", file.Path), "Saved successfully!");
                await infoPopup.ShowAsync();
            }
        }

        private async void Load_Pressed(object sender, PointerRoutedEventArgs e )
        {
            var fileOpener = new FileOpenPicker();
            fileOpener.FileTypeFilter.Add(".json");
            var file = await fileOpener.PickSingleFileAsync();
            if (file != null)
            {
                using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    using (var inputStream = fileStream.GetInputStreamAt(0))
                    {
                        using (var streamReader = new StreamReader(inputStream.AsStreamForRead()))
                        {
                            string dataString = await streamReader.ReadToEndAsync();
                            if (deserialize(dataString) == false)
                            {
                                MessageDialog infoPopup = new MessageDialog(
                                        String.Format("Path: {0}. Not all entities could be loaded.", file.Path),
                                        "Failed to load complete diagram.");
                                await infoPopup.ShowAsync();
                            }
                            
                        }
                        inputStream.Dispose();
                    }
                }
            }
        }

        private async Task<string> ExportPNG()
        {
            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(workspace);
            var pixelBuffer = await renderTargetBitmap.GetPixelsAsync();

            var fileSaver = new FileSavePicker();
            fileSaver.FileTypeChoices.Add("Image", new List<String>{".png", ".jpeg", ".jpg", ".bmp", ".gif", ".tiff"});
            var file = await fileSaver.PickSaveFileAsync();
            if (file != null)
            { // see: http://loekvandenouweland.com/index.php/2013/12/save-xaml-as-png-in-a-windows-store-app/
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoderId =
                        (file.FileType == "jpeg" || file.FileType == "jpg") ? BitmapEncoder.JpegEncoderId :
                        (file.FileType == "bmp") ? BitmapEncoder.BmpEncoderId :
                        (file.FileType == "gif") ? BitmapEncoder.GifEncoderId :
                        (file.FileType == "tiff") ? BitmapEncoder.TiffEncoderId :
                        BitmapEncoder.PngEncoderId; // default
                    var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                                         BitmapAlphaMode.Ignore,
                                         (uint)renderTargetBitmap.PixelWidth,
                                         (uint)renderTargetBitmap.PixelHeight, 96d, 96d,
                                         pixelBuffer.ToArray());
                    await encoder.FlushAsync();
                }
                return file.Path;
            }
            return null;
        }

        private async void Export_Pressed(object sender, PointerRoutedEventArgs e)
        {
            var path = await ExportPNG();
            if (path != null)
            {
                MessageDialog infoPopup = new MessageDialog(String.Format("Saved to file {0}", path), "Saved successfully");
                await infoPopup.ShowAsync();
            }
        }

        private async void Clear_Pressed(object sender, PointerRoutedEventArgs e)
        {
            await clearWorkspace();
        }

        private async Task<bool> clearWorkspace()
        {
            MessageDialog confirmDialog = new MessageDialog("", String.Format("Really clear workspace?"));
            confirmDialog.Commands.Add(new UICommand("Yes"));
            confirmDialog.Commands.Add(new UICommand("Cancel"));
            var result = await confirmDialog.ShowAsync();
            if (result.Label == "Yes")
            {
                workspace.Children.Clear();
                links.Clear();
                controls.Clear();
                Warnings.Clear();
                BaseControl.resetIds();
                return true;
            }
            return false;
        }

        private void warnings_tab_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var warningsRow = warnings_grid.RowDefinitions[1];
            var newHeight = Math.Min(Math.Max(warningsRow.ActualHeight - e.Delta.Translation.Y, 0), workspace.ActualHeight - warnings_tab.ActualHeight);
            warningsRow.Height = new GridLength(newHeight, GridUnitType.Pixel);
            warningsPaneSize = newHeight;
        }

        private void minimize_button_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (warnings_grid.RowDefinitions[1].ActualHeight != 0)
            {
                warnings_grid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel);
            }
            else
            {
                warnings_grid.RowDefinitions[1].Height = new GridLength(warningsPaneSize, GridUnitType.Pixel);
            }
        }

        private void AddWarnings(object sender, ObservableCollection<WarningItem> added)
        {
            foreach (var item in added)
            {
                if (Warnings.Contains(item) == false)
                { // TODO: filter out duplicate warnings at validation time
                    Warnings.Add(item);
                }
            }
        }

        private void RemoveWarnings(object sender, ObservableCollection<WarningItem> removed)
        {
            foreach (var item in removed)
            {
                if (Warnings.Contains(item))
                {
                    Warnings.Remove(item);
                }
            }
        }

        private List<Link> findLinks(BaseControl control)
        {
            var result = new List<Link>();
            foreach (var link in links)
            {
                if (link.sourceControl == control || link.targetControl == control)
                {
                    result.Add(link);
                }
            }
            return result;
        }

        private void warnings_listview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var selected = (WarningItem)e.AddedItems[0];
                if (selected != null)
                {
                    selected.control.HighLight();
                }
            }

            if (e.RemovedItems.Count > 0)
            {
                var released = (WarningItem)e.RemovedItems[0];
                if (released != null)
                {
                    released.control.LowLight();
                }
            }
        }
    }
}
