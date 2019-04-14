// 
// FileDialog.cs: File system dialogs for open and save
//
// TODO:
//   * Add directory selector
//   * Implement subclasses
//   * Figure out why message text does not show
//   * Remove the extra space when message does not show
//   * Use a line separator to show the file listing, so we can use same colors as the rest
//   * DirListView: Add mouse support

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NStack;

#pragma warning disable 1591

// ReSharper disable CommentTypo

namespace Terminal.Gui {
	class DirListView : View {
		string[] _allowedFileTypes;
		internal bool AllowsMultipleSelection;
		internal bool CanChooseDirectories;
		internal bool CanChooseFiles = true;

		ustring _directory;
		public Action<ustring> DirectoryChanged;
		DirectoryInfo _dirInfo;
		public Action<ustring> FileChanged;
		List<(string text, bool isDirectory, bool isSelected)> _infos;

		public Action<(string, bool)> SelectedChanged;
		private int _top, _selected;

		public DirListView()
		{
			_infos = new List<(string text, bool isDirectory, bool isSelected)>();
			CanFocus = true;
		}

		public ustring Directory {
			get => _directory;
			set {
				if (_directory == value)
					return;
				_directory = value;
				Reload();
			}
		}

		public string[] AllowedFileTypes {
			get => _allowedFileTypes;
			set {
				_allowedFileTypes = value;
				Reload();
			}
		}

		public IReadOnlyList<string> FilePaths {
			get {
				if (AllowsMultipleSelection) {
					var res = new List<string>();
					foreach (var item in _infos)
						if (item.isSelected && item.isDirectory == CanChooseDirectories &&
						    !item.isDirectory == CanChooseFiles)
							res.Add(MakePath(item.text));
					/*if (!res.Any() && _infos[_selected].isDirectory == CanChooseDirectories &&
					    !_infos[_selected].isDirectory == CanChooseFiles) {
						res.Add(MakePath(_infos[_selected].text));
					}
*/

					return res;
				}

				if (_infos[_selected].isDirectory) {
					if (CanChooseDirectories)
						return new List<string> {MakePath(_infos[_selected].text)};
					return Array.Empty<string>();
				}

				if (CanChooseFiles)
					return new List<string> {MakePath(_infos[_selected].text)};
				return Array.Empty<string>();
			}
		}

		bool IsAllowed(FileSystemInfo fsi)
		{
			if (fsi.Attributes.HasFlag(FileAttributes.Directory))
				return true;
			if (_allowedFileTypes == null)
				return true;
			foreach (var ft in _allowedFileTypes)
				if (fsi.Name.EndsWith(ft))
					return true;
			return false;
		}

		internal void Reload()
		{
			_dirInfo = new DirectoryInfo(_directory.ToString());
			_infos = (from x in _dirInfo.GetFileSystemInfos()
				where IsAllowed(x)
				orderby !x.Attributes.HasFlag(FileAttributes.Directory) + x.Name
				select (x.Name, x.Attributes.HasFlag(FileAttributes.Directory), false)).ToList();
			_infos.Insert(0, ("..", true, false));
			_top = 0;
			_selected = 0;
			SetNeedsDisplay();
		}

		public override void PositionCursor()
		{
			Move(0, _selected - _top);
		}

		void DrawString(int line, string str)
		{
			var f = Frame;
			var width = f.Width;
			var ustr = ustring.Make(str);

			Move(AllowsMultipleSelection ? 3 : 2, line);
			var byteLen = ustr.Length;
			var used = 0;
			for (var i = 0; i < byteLen;) {
				var (rune, size) = Utf8.DecodeRune(ustr, i, i - byteLen);
				var count = Rune.ColumnWidth(rune);
				if (used + count >= width)
					break;
				Driver.AddRune(rune);
				used += count;
				i += size;
			}

			for (; used < width; used++) Driver.AddRune(' ');
		}

		public override void Redraw(Rect region)
		{
			var current = ColorScheme.Focus;
			Driver.SetAttribute(current);
			Move(0, 0);
			var f = Frame;
			var item = _top;
			var focused = HasFocus;
			var width = region.Width;

			for (var row = 0; row < f.Height; row++, item++) {
				var isSelected = item == _selected;
				Move(0, row);
				var newcolor = focused ? isSelected ? ColorScheme.HotNormal : ColorScheme.Focus : ColorScheme.Focus;
				if (newcolor != current) {
					Driver.SetAttribute(newcolor);
					current = newcolor;
				}

				if (item >= _infos.Count) {
					for (var c = 0; c < f.Width; c++)
						Driver.AddRune(' ');
					continue;
				}

				var fi = _infos[item];

				Driver.AddRune(isSelected ? '>' : ' ');

				if (AllowsMultipleSelection)
					Driver.AddRune(fi.isSelected ? '*' : ' ');

				if (fi.isDirectory)
					Driver.AddRune('/');
				else
					Driver.AddRune(' ');
				DrawString(row, fi.text);
			}
		}

		void SelectionChanged()
		{
			if (SelectedChanged != null) {
				var sel = _infos[_selected];
				SelectedChanged((sel.text, sel.isDirectory));
			}
		}

		public override bool ProcessKey(KeyEvent keyEvent)
		{
			switch (keyEvent.Key) {
			case Key.CursorUp:
			case Key.ControlP:
				if (_selected > 0) {
					_selected--;
					if (_selected < _top)
						_top = _selected;
					SelectionChanged();
					SetNeedsDisplay();
				}

				return true;

			case Key.CursorDown:
			case Key.ControlN:
				if (_selected + 1 < _infos.Count) {
					_selected++;
					if (_selected >= _top + Frame.Height)
						_top++;
					SelectionChanged();
					SetNeedsDisplay();
				}

				return true;

			case Key.ControlV:
			case Key.PageDown:
				var n = _selected + Frame.Height;
				if (n > _infos.Count)
					n = _infos.Count - 1;
				if (n != _selected) {
					_selected = n;
					if (_infos.Count >= Frame.Height)
						_top = _selected;
					else
						_top = 0;
					SelectionChanged();

					SetNeedsDisplay();
				}

				return true;

			case Key.Enter:
				var isDir = _infos[_selected].isDirectory;

				if (isDir) {
					Directory = Path.GetFullPath(Path.Combine(Path.GetFullPath(Directory.ToString()), _infos[_selected].text));
					if (DirectoryChanged != null)
						DirectoryChanged(Directory);
				} else {
					if (FileChanged != null)
						FileChanged(_infos[_selected].text);
					if (CanChooseFiles) return false;

					// No files allowed, do not let the default handler take it.
				}

				return true;

			case Key.PageUp:
				n = _selected - Frame.Height;
				if (n < 0)
					n = 0;
				if (n != _selected) {
					_selected = n;
					_top = _selected;
					SelectionChanged();
					SetNeedsDisplay();
				}

				return true;

			case Key.Space:
			case Key.ControlT:
				if (AllowsMultipleSelection)
					if (CanChooseFiles && _infos[_selected].isDirectory == false ||
					    CanChooseDirectories && _infos[_selected].isDirectory &&
					    _infos[_selected].text != "..") {
						_infos[_selected] = (_infos[_selected].text, _infos[_selected].isDirectory,
							!_infos[_selected].isSelected);
						SelectionChanged();
						SetNeedsDisplay();
					}

				return true;
			}

			return base.ProcessKey(keyEvent);
		}

		public string MakePath(string relativePath)
		{
			return Path.GetFullPath(Path.Combine(Directory.ToString(), relativePath));
		}
	}

	/// <summary>
	///     Base class for the OpenDialog and the SaveDialog
	/// </summary>
	public class FileDialog : Dialog {
		readonly TextField _dirEntry;
		readonly Label _message;
		readonly TextField _nameEntry;
		readonly Label _nameFieldLabel;
		readonly Button _prompt;
		internal readonly DirListView DirListView;

		internal bool Canceled;

		public FileDialog(ustring title, ustring prompt, ustring nameFieldLabel, ustring message) : base(title, Driver.Cols - 20,
			Driver.Rows - 5, null)
		{
			_message = new Label(Rect.Empty, "MESSAGE" + message);
			var msgLines = Label.MeasureLines(message, Driver.Cols - 20);

			var dirLabel = new Label("Directory: ") {
				X = 1,
				Y = 1 + msgLines
			};

			_dirEntry = new TextField("") {
				X = Pos.Right(dirLabel),
				Y = 1 + msgLines,
				Width = Dim.Fill() - 1
			};
			Add(dirLabel, _dirEntry);

			_nameFieldLabel = new Label("Open: ") {
				X = 6,
				Y = 3 + msgLines
			};
			_nameEntry = new TextField("") {
				X = Pos.Left(_dirEntry),
				Y = 3 + msgLines,
				Width = Dim.Fill() - 1
			};
			Add(_nameFieldLabel, _nameEntry);

			DirListView = new DirListView {
				X = 1,
				Y = 3 + msgLines + 2,
				Width = Dim.Fill() - 2,
				Height = Dim.Fill() - 2
			};
			DirectoryPath = Path.GetFullPath(Environment.CurrentDirectory);
			Add(DirListView);
			DirListView.DirectoryChanged = dir => _dirEntry.Text = dir;
			DirListView.FileChanged = file => { _nameEntry.Text = file; };

			var cancel = new Button("Cancel");
			cancel.Clicked += () => {
				Canceled = true;
				Application.RequestStop();
			};
			AddButton(cancel);

			_prompt = new Button(prompt) {
				IsDefault = true
			};
			_prompt.Clicked += () => {
				Canceled = false;
				Application.RequestStop();
			};
			AddButton(_prompt);

			// On success, we will set this to false.
			Canceled = true;
		}

		/// <summary>
		///     Gets or sets the prompt label for the button displayed to the user
		/// </summary>
		/// <value>The prompt.</value>
		public ustring Prompt {
			get => _prompt.Text;
			set => _prompt.Text = value;
		}

		/// <summary>
		///     Gets or sets the name field label.
		/// </summary>
		/// <value>The name field label.</value>
		public ustring NameFieldLabel {
			get => _nameFieldLabel.Text;
			set => _nameFieldLabel.Text = value;
		}

		/// <summary>
		///     Gets or sets the message displayed to the user, defaults to nothing
		/// </summary>
		/// <value>The message.</value>
		public ustring Message {
			get => _message.Text;
			set => _message.Text = value;
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.FileDialog" /> can create directories.
		/// </summary>
		/// <value><c>true</c> if can create directories; otherwise, <c>false</c>.</value>
		public bool CanCreateDirectories { get; set; }

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.FileDialog" /> is extension hidden.
		/// </summary>
		/// <value><c>true</c> if is extension hidden; otherwise, <c>false</c>.</value>
		public bool IsExtensionHidden { get; set; }

		/// <summary>
		///     Gets or sets the directory path for this panel
		/// </summary>
		/// <value>The directory path.</value>
		public ustring DirectoryPath {
			get => _dirEntry.Text;
			set {
				_dirEntry.Text = value;
				DirListView.Directory = value;
			}
		}

		/// <summary>
		///     The array of filename extensions allowed, or null if all file extensions are allowed.
		/// </summary>
		/// <value>The allowed file types.</value>
		public string[] AllowedFileTypes {
			get => DirListView.AllowedFileTypes;
			set => DirListView.AllowedFileTypes = value;
		}


		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.FileDialog" /> allows the file to be saved
		///     with a different extension
		/// </summary>
		/// <value><c>true</c> if allows other file types; otherwise, <c>false</c>.</value>
		public bool AllowsOtherFileTypes { get; set; }

		/// <summary>
		///     The File path that is currently shown on the panel
		/// </summary>
		/// <value>The absolute file path for the file path entered.</value>
		public ustring FilePath {
			get => _nameEntry.Text;
			set => _nameEntry.Text = value;
		}
	}

	/// <summary>
	///     The save dialog provides an interactive dialog box for users to pick a file to
	///     save.
	/// </summary>
	/// <remarks>
	///     <para>
	///         To use it, create an instance of the SaveDialog, and then
	///         call Application.Run on the resulting instance.   This will run the dialog modally,
	///         and when this returns, the FileName property will contain the selected value or
	///         null if the user canceled.
	///     </para>
	/// </remarks>
	public class SaveDialog : FileDialog {
		public SaveDialog(ustring title, ustring message) : base(title, "Save", "Save as:", message)
		{
		}

		/// <summary>
		///     Gets the name of the file the user selected for saving, or null
		///     if the user canceled the dialog box.
		/// </summary>
		/// <value>The name of the file.</value>
		public ustring FileName {
			get {
				if (Canceled)
					return null;
				return FilePath;
			}
		}
	}

	/// <summary>
	///     The Open Dialog provides an interactive dialog box for users to select files or directories.
	/// </summary>
	/// <remarks>
	///     <para>
	///         The open dialog can be used to select files for opening, it can be configured to allow
	///         multiple items to be selected (based on the AllowsMultipleSelection) variable and
	///         you can control whether this should allow files or directories to be selected.
	///     </para>
	///     <para>
	///         To use it, create an instance of the OpenDialog, configure its properties, and then
	///         call Application.Run on the resulting instance.   This will run the dialog modally,
	///         and when this returns, the list of fields will be available on the FilePaths property.
	///     </para>
	///     <para>
	///         To select more than one file, users can use the spacebar, or control-t.
	///     </para>
	/// </remarks>
	public class OpenDialog : FileDialog {
		public OpenDialog(ustring title, ustring message) : base(title, "Open", "Open", message)
		{
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.OpenDialog" /> can choose files.
		/// </summary>
		/// <value><c>true</c> if can choose files; otherwise, <c>false</c>.  Defaults to <c>true</c></value>
		public bool CanChooseFiles {
			get => DirListView.CanChooseFiles;
			set {
				DirListView.CanChooseDirectories = value;
				DirListView.Reload();
			}
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.OpenDialog" /> can choose directories.
		/// </summary>
		/// <value><c>true</c> if can choose directories; otherwise, <c>false</c> defaults to <c>false</c>.</value>
		public bool CanChooseDirectories {
			get => DirListView.CanChooseDirectories;
			set {
				DirListView.CanChooseDirectories = value;
				DirListView.Reload();
			}
		}

		/// <summary>
		///     Gets or sets a value indicating whether this <see cref="T:Terminal.Gui.OpenDialog" /> allows multiple selection.
		/// </summary>
		/// <value><c>true</c> if allows multiple selection; otherwise, <c>false</c>, defaults to false.</value>
		public bool AllowsMultipleSelection {
			get => DirListView.AllowsMultipleSelection;
			set {
				DirListView.AllowsMultipleSelection = value;
				DirListView.Reload();
			}
		}

		/// <summary>
		///     Returns the selected files, or an empty list if nothing has been selected
		/// </summary>
		/// <value>The file paths.</value>
		public IReadOnlyList<string> FilePaths => DirListView.FilePaths;
	}
}