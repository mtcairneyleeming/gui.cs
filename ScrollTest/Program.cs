using Terminal.Gui;

namespace ScrollTest {
	class Program {
		static FrameView GenFrame(int yCoord)
		{
			var optionsArr = new[] {"One", "Two", "Three"};
			var frame = new FrameView("A generated frame") {
				X = 1,
				Y = yCoord,
				Width = 60,
				Height = optionsArr.Length + 6
			};
			var categoryRadio = new RadioGroup(1, 1, optionsArr);
			frame.Add(categoryRadio);
			frame.Add(new Button("Check") {
				Y = optionsArr.Length + 2,
				X = 1,
				Width = 7,
				Height = 1
			});
			return frame;
		}

		public static void Main()
		{
			Application.Init();
			var top = Application.Top;

			var window = new Window("Test") {
				X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill()
			};
			
			window.Add(new Button("X") {
				Clicked = Application.RequestStop,
				X = Pos.Right(window) - 8,
				Y = 1,
				Width = 5,
				Height = 1
			});

			var generatedQuestionFrame = GenFrame(1);
			window.Add(generatedQuestionFrame);
			var generatedQuestionFrame2 = GenFrame(10);
			window.Add(generatedQuestionFrame2);

			window.Add(new Button("Finish") {
				X = 1,
				Y = 30,
				Width = 8,
				Height = 1
			});
			Application.Run(window);
		}
	}
}