using Dapr.Actors;
using Dapr.Actors.Client;
using ilionx.MicroServices.Actors.Interface;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ilionx.MicroServices.ClientApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        internal static bool isClosing = false;
        private DispatcherTimer refreshTimer = null;
        private static object shapesLock = new object();
        private static object timerLock = new object();
        private bool busyRefresh = false;
        private readonly IShapeListService shapeListService;
        private readonly IShapeEventHandler shapeEventHandler;

        /// <summary>
        /// All shapes.
        /// </summary>
        private readonly Dictionary<Guid, ShapeState> shapes = new Dictionary<Guid, ShapeState>();

        /// <summary>
        /// Gets the client ID from the app settings.
        /// </summary>
        private Guid ClientId
        {
            get
            {
                return Guid.Parse(ConfigurationManager.AppSettings["clientId"]);
            }
        }

        /// <summary>
        /// Shape state.
        /// </summary>
        private class ShapeState
        {
            /// <summary>
            /// The shape.
            /// </summary>
            public IShapeActor Shape { get; private set; }

            /// <summary>
            /// The shape event handler.
            /// </summary>
            public ShapeEventsHandler ShapeEventHandler { get; private set; }

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="shape">The shape.</param>
            /// <param name="shapeEventHandler">The shape event handler.</param>
            public ShapeState(IShapeActor shape, ShapeEventsHandler shapeEventHandler)
            {
                this.Shape = shape;
                this.ShapeEventHandler = shapeEventHandler;
            }

            /// <summary>
            /// Unsubscribe to the shape event handler.
            /// </summary>
            /// <returns>Async task.</returns>
            public async Task UnsubscribeAsync()
            {
                await this.Shape.UnregisterReminder();
            }
        }

        /// <summary>
        /// Handler for ShapeEvents.
        /// </summary>
        private class ShapeEventsHandler : IShapeEvents
        {
            private readonly Canvas shapesCanvas;

            public Rectangle UiShape { get; private set; }

            /// <summary>
            /// Default constructor.
            /// </summary>
            /// <param name="shapesCanvas">The shaped canvas to draw shapes on.</param>
            /// <param name="uiShape">The UI shape element.</param>
            public ShapeEventsHandler(Canvas shapesCanvas, Rectangle uiShape)
            {
                this.shapesCanvas = shapesCanvas;
                this.UiShape = uiShape;
            }

            /// <summary>
            /// ShapeChanged event. A new shape position is sent.
            /// </summary>
            /// <param name="shape">The new shape position.</param>
            public async void ShapeChanged(Models.Shape shape)
            {
                if (isClosing)
                {
                    return;
                }

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    Canvas.SetTop(UiShape, shape.Y);
                    Canvas.SetLeft(UiShape, shape.X);
                    ((RotateTransform)UiShape.RenderTransform).Angle = shape.Angle;

                    if (!shapesCanvas.Children.Contains(UiShape))
                    {
                        shapesCanvas.Children.Add(UiShape);
                    }
                });
            }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="shapeListService"></param>
        /// <param name="shapeEventHandler"></param>
        public MainWindow(IShapeListService shapeListService, IShapeEventHandler shapeEventHandler)
        {
            // save the provided instances
            this.shapeListService = shapeListService;
            this.shapeEventHandler = shapeEventHandler;
            this.shapeEventHandler.OnUpdatedShapeLocation += async (sender, shapeId) => await ShapeEventHandler_OnUpdatedShapeLocation(shapeId);

            // initialize xaml form component
            InitializeComponent();

            // need a timer to validate the callback connection to the shape actors (lost during upgrade/failover of services cluster)
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromMilliseconds(1000);
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        /// <summary>
        /// Event called before closing the window.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isClosing = true;

            // stop timer
            if (refreshTimer != null)
            {
                refreshTimer.Stop();
                refreshTimer = null;
            }

            // unsubscribe to all shape change events.
            foreach (Guid shapeId in shapes.Keys)
            {
                await shapes[shapeId].UnsubscribeAsync();
            }

            // unsubscibe from SignalR
            await shapeEventHandler.Disconnect().ConfigureAwait(false);
        }

        /// <summary>
        /// Event called after the window has loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // subscibe on shape events from SignalR
                await shapeEventHandler.SubscribeOnUpdatedShapeLocation().ConfigureAwait(false);

                // get the shape list for this client (active shape actors)
                var shapeList = await shapeListService.GetAll(ClientId);

                // restore the shapes
                await Task.WhenAll(shapeList.Select(shapeId => CreateShape(shapeId)));
            }
            catch (Exception ex)
            {
                string errorText = GetExceptionMessageText(ex);
                MessageBox.Show(errorText);
            }
        }

        /// <summary>
        /// AddShapeButton click event. Add a new shape to the canvas.
        /// </summary>
        /// <param name="sender">Sender of the event.</param>
        /// <param name="e">Event paraeters.</param>
        private async void AddShapeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // create a new shape ID
                Guid shapeId = Guid.NewGuid();

                // create a new shape
                await CreateShape(shapeId);
            }
            catch (Exception ex)
            {
                string errorText = GetExceptionMessageText(ex);
                MessageBox.Show(errorText);
            }
        }

        /// <summary>
        /// Event called when the actor notifies the shape location hased changed.
        ///  -> [Actor: new location]
        ///  -> [Actor: publish changed event]
        ///  -> [ShapeList: subscribe changed event]
        ///  -> [ShapeList: publish changed event via SignalR]
        ///  -> [XamlClient: subscribe changed event]
        ///  -> [XamlClient: get changed location from actor]
        ///  -> [XamlClient: update shape location on canvas]
        /// </summary>
        /// <param name="shapeId"></param>
        /// <returns></returns>
        private async Task ShapeEventHandler_OnUpdatedShapeLocation(Guid shapeId)
        {
            if (shapes.ContainsKey(shapeId))
            {
                // get the changed location from the shape actor service
                var currentShape = await shapes[shapeId].Shape.GetCurrentPositionAsync();

                // refresh the shape location on the canvas
                shapes[shapeId].ShapeEventHandler.ShapeChanged(currentShape);
            }
        }

        /// <summary>
        /// Creates a shape and adds it to the canvas.
        /// </summary>
        /// <param name="shapeId">The ID of the shape to add.</param>
        /// <returns>Async task.</returns>
        private async Task CreateShape(Guid shapeId)
        {
            // create a shape UI element
            Rectangle shape = null;
            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                RotateTransform rotation = new RotateTransform() { Angle = 0, CenterX = 50, CenterY = 50 };
                shape = new Rectangle()
                {
                    Height = 100,
                    Width = 100,
                    Stroke = new SolidColorBrush(Colors.Yellow),
                    StrokeThickness = 5,
                    RadiusX = 20,
                    RadiusY = 20,
                    RenderTransform = rotation
                };
            });

            // get the current shape position from the shape actor service
            ActorId actorId = new ActorId($"{ClientId:N}_{shapeId:N}");
            IShapeActor shapeActor = ActorProxy.Create<IShapeActor>(actorId, "ShapeActor");
            var currentShape = await shapeActor.GetCurrentPositionAsync();

            // subscribe this shape to all shape change events
            var shapesEventHandler = new ShapeEventsHandler(ShapesCanvas, shape);

            // save the shape state
            shapes.Add(shapeId, new ShapeState(shapeActor, shapesEventHandler));
            shapesEventHandler.ShapeChanged(currentShape);
        }

        /// <summary>
        /// Timer tick event to refresh actors.
        /// Needed when upgrading cluster.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (busyRefresh) return;

            lock (timerLock)
            {
                busyRefresh = true;
            }

            List<Guid> shapeIds;
            lock (shapesLock)
            {
                shapeIds = shapes.Keys.ToList();
            }

            foreach (Guid shapeId in shapeIds)
            {
                try
                {
                    var currentShape = await shapes[shapeId].Shape.GetCurrentPositionAsync();
                }
                catch (Exception)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (ShapesCanvas.Children.Contains(shapes[shapeId].ShapeEventHandler.UiShape))
                        {
                            ShapesCanvas.Children.Remove(shapes[shapeId].ShapeEventHandler.UiShape);
                        }
                    });

                    lock (shapesLock)
                    {
                        shapes.Remove(shapeId);
                    }
                }
            }

            lock (timerLock)
            {
                busyRefresh = false;
            }
        }

        /// <summary>
        /// Gets the error message from the provided exception.
        /// </summary>
        /// <param name="ex">Exception</param>
        /// <returns>The error message.</returns>
        private string GetExceptionMessageText(Exception ex)
        {
            string errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += String.Format("\r\nInner exception:\r\n{0}", GetExceptionMessageText(ex.InnerException));
            }

            return errorMessage;
        }
    }
}
