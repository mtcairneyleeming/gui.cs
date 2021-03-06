﻿//
// TextView.cs: multi-line text editing
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// 
// TODO:
// In ReadOnly mode backspace/space behave like pageup/pagedown
// Attributed text on spans
// Replace insertion with Insert method
// String accumulation (Control-k, control-k is not preserving the last new line, see StringToRunes
// Alt-D, Alt-Backspace
// API to set the cursor position
// API to scroll to a particular place
// keybindings to go to top/bottom
// public API to insert, remove ranges
// Add word forward/word backwards commands
// Save buffer API
// Mouse
//
// Desirable:
//   Move all the text manipulation into the TextModel


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NStack;

namespace Terminal.Gui {
	class TextModel {
		List<List<Rune>> lines = new List<List<Rune>>();

		/// <summary>
		///     The number of text lines in the model
		/// </summary>
		public int Count => lines.Count;

		public bool LoadFile(string file)
		{
			if (file == null)
				throw new ArgumentNullException(nameof(file));
			try {
				var stream = File.OpenRead(file);
			} catch {
				return false;
			}

			LoadStream(File.OpenRead(file));
			return true;
		}

		// Turns the ustring into runes, this does not split the 
		// contents on a newline if it is present.
		internal static List<Rune> ToRunes(ustring str)
		{
			var runes = new List<Rune>();
			foreach (var x in str.ToRunes()) runes.Add(x);
			return runes;
		}

		// Splits a string into a List that contains a List<Rune> for each line
		public static List<List<Rune>> StringToRunes(ustring content)
		{
			var lines = new List<List<Rune>>();
			int start = 0, i = 0;
			for (; i < content.Length; i++)
				if (content[i] == 10) {
					if (i - start > 0)
						lines.Add(ToRunes(content[start, i]));
					else
						lines.Add(ToRunes(ustring.Empty));
					start = i + 1;
				}

			if (i - start >= 0)
				lines.Add(ToRunes(content[start, null]));
			return lines;
		}

		void Append(List<byte> line)
		{
			var str = ustring.Make(line.ToArray());
			lines.Add(ToRunes(str));
		}

		public void LoadStream(Stream input)
		{
			if (input == null)
				throw new ArgumentNullException(nameof(input));

			lines = new List<List<Rune>>();
			var buff = new BufferedStream(input);
			int v;
			var line = new List<byte>();
			while ((v = buff.ReadByte()) != -1) {
				if (v == 10) {
					Append(line);
					line.Clear();
					continue;
				}

				line.Add((byte) v);
			}

			if (line.Count > 0)
				Append(line);
		}

		public void LoadString(ustring content)
		{
			lines = StringToRunes(content);
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			foreach (var line in lines) {
				sb.Append(ustring.Make(line));
				sb.AppendLine();
			}

			return sb.ToString();
		}

		/// <summary>
		///     Returns the specified line as a List of Rune
		/// </summary>
		/// <returns>The line.</returns>
		/// <param name="line">Line number to retrieve.</param>
		public List<Rune> GetLine(int line)
		{
			return line < Count ? lines[line] : lines[Count - 1];
		}

		/// <summary>
		///     Adds a line to the model at the specified position.
		/// </summary>
		/// <param name="pos">Line number where the line will be inserted.</param>
		/// <param name="runes">The line of text, as a List of Rune.</param>
		public void AddLine(int pos, List<Rune> runes)
		{
			lines.Insert(pos, runes);
		}

		/// <summary>
		///     Removes the line at the specified position
		/// </summary>
		/// <param name="pos">Position.</param>
		public void RemoveLine(int pos)
		{
			lines.RemoveAt(pos);
		}
	}

	/// <summary>
	///     Multi-line text editing view
	/// </summary>
	/// <remarks>
	///     <para>
	///         The text view provides a multi-line text view.   Users interact
	///         with it with the standard Emacs commands for movement or the arrow
	///         keys.
	///     </para>
	///     <list type="table">
	///         <listheader>
	///             <term>Shortcut</term>
	///             <description>Action performed</description>
	///         </listheader>
	///         <item>
	///             <term>Left cursor, Control-b</term>
	///             <description>
	///                 Moves the editing point left.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Right cursor, Control-f</term>
	///             <description>
	///                 Moves the editing point right.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Alt-b</term>
	///             <description>
	///                 Moves one word back.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Alt-f</term>
	///             <description>
	///                 Moves one word forward.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Up cursor, Control-p</term>
	///             <description>
	///                 Moves the editing point one line up.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Down cursor, Control-n</term>
	///             <description>
	///                 Moves the editing point one line down
	///             </description>
	///         </item>
	///         <item>
	///             <term>Home key, Control-a</term>
	///             <description>
	///                 Moves the cursor to the beginning of the line.
	///             </description>
	///         </item>
	///         <item>
	///             <term>End key, Control-e</term>
	///             <description>
	///                 Moves the cursor to the end of the line.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Delete, Control-d</term>
	///             <description>
	///                 Deletes the character in front of the cursor.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Backspace</term>
	///             <description>
	///                 Deletes the character behind the cursor.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Control-k</term>
	///             <description>
	///                 Deletes the text until the end of the line and replaces the kill buffer
	///                 with the deleted text.   You can paste this text in a different place by
	///                 using Control-y.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Control-y</term>
	///             <description>
	///                 Pastes the content of the kill ring into the current position.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Alt-d</term>
	///             <description>
	///                 Deletes the word above the cursor and adds it to the kill ring.  You
	///                 can paste the contents of the kill ring with Control-y.
	///             </description>
	///         </item>
	///         <item>
	///             <term>Control-q</term>
	///             <description>
	///                 Quotes the next input character, to prevent the normal processing of
	///                 key handling to take place.
	///             </description>
	///         </item>
	///     </list>
	/// </remarks>
	public class TextView : View {
		readonly TextModel model = new TextModel();
		int topRow;
		int leftColumn;
		int selectionStartColumn, selectionStartRow;

		bool selecting;
		//bool used;

#if false
		/// <summary>
		///   Changed event, raised when the text has clicked.
		/// </summary>
		/// <remarks>
		///   Client code can hook up to this event, it is
		///   raised when the text in the entry changes.
		/// </remarks>
		public event EventHandler Changed;
#endif
		/// <summary>
		///     Public constructor, creates a view on the specified area, with absolute position and size.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public TextView(Rect frame) : base(frame)
		{
			CanFocus = true;
		}

		/// <summary>
		///     Public constructor, creates a view on the specified area, with dimensions controlled with the X, Y, Width and
		///     Height properties.
		/// </summary>
		public TextView()
		{
			CanFocus = true;
		}

		void ResetPosition()
		{
			topRow = leftColumn = CurrentRow = CurrentColumn = 0;
		}

		/// <summary>
		///     Sets or gets the text in the entry.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public ustring Text {
			get => model.ToString();

			set {
				ResetPosition();
				model.LoadString(value);
				SetNeedsDisplay();
			}
		}

		/// <summary>
		///     Loads the contents of the file into the TextView.
		/// </summary>
		/// <returns><c>true</c>, if file was loaded, <c>false</c> otherwise.</returns>
		/// <param name="path">Path to the file to load.</param>
		public bool LoadFile(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));
			ResetPosition();
			var res = model.LoadFile(path);
			SetNeedsDisplay();
			return res;
		}

		/// <summary>
		///     Loads the contents of the stream into the TextView.
		/// </summary>
		/// <returns><c>true</c>, if stream was loaded, <c>false</c> otherwise.</returns>
		/// <param name="stream">Stream to load the contents from.</param>
		public void LoadStream(Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));
			ResetPosition();
			model.LoadStream(stream);
			SetNeedsDisplay();
		}

		/// <summary>
		///     The current cursor row.
		/// </summary>
		public int CurrentRow { get; private set; }

		/// <summary>
		///     Gets the cursor column.
		/// </summary>
		/// <value>The cursor column.</value>
		public int CurrentColumn { get; private set; }

		/// <summary>
		///     Positions the cursor on the current row and column
		/// </summary>
		public override void PositionCursor()
		{
			if (selecting) {
				var minRow = Math.Min(Math.Max(Math.Min(selectionStartRow, CurrentRow) - topRow, 0), Frame.Height);
				var maxRow = Math.Min(Math.Max(Math.Max(selectionStartRow, CurrentRow) - topRow, 0), Frame.Height);

				SetNeedsDisplay(new Rect(0, minRow, Frame.Width, maxRow));
			}

			Move(CurrentColumn - leftColumn, CurrentRow - topRow);
		}

		void ClearRegion(int left, int top, int right, int bottom)
		{
			for (var row = top; row < bottom; row++) {
				Move(left, row);
				for (var col = left; col < right; col++)
					AddRune(col, row, ' ');
			}
		}

		void ColorNormal()
		{
			Driver.SetAttribute(ColorScheme.Normal);
		}

		void ColorSelection()
		{
			if (HasFocus)
				Driver.SetAttribute(ColorScheme.Focus);
			else
				Driver.SetAttribute(ColorScheme.Normal);
		}

		/// <summary>
		///     Indicates readonly attribute of TextView
		/// </summary>
		/// <value>Boolean value(Default false)</value>
		public bool ReadOnly { get; set; } = false;

		// Returns an encoded region start..end (top 32 bits are the row, low32 the column)
		void GetEncodedRegionBounds(out long start, out long end)
		{
			var selection = ((long) (uint) selectionStartRow << 32) | (uint) selectionStartColumn;
			var point = ((long) (uint) CurrentRow << 32) | (uint) CurrentColumn;
			if (selection > point) {
				start = point;
				end = selection;
			} else {
				start = selection;
				end = point;
			}
		}

		bool PointInSelection(int col, int row)
		{
			long start, end;
			GetEncodedRegionBounds(out start, out end);
			var q = ((long) (uint) row << 32) | (uint) col;
			return q >= start && q <= end;
		}

		//
		// Returns a ustring with the text in the selected 
		// region.
		//
		ustring GetRegion()
		{
			long start, end;
			GetEncodedRegionBounds(out start, out end);
			var startRow = (int) (start >> 32);
			var maxrow = (int) (end >> 32);
			var startCol = (int) (start & 0xffffffff);
			var endCol = (int) (end & 0xffffffff);
			var line = model.GetLine(startRow);

			if (startRow == maxrow)
				return StringFromRunes(line.GetRange(startCol, endCol));

			var res = StringFromRunes(line.GetRange(startCol, line.Count - startCol));

			for (var row = startRow + 1; row < maxrow; row++) res = res + ustring.Make((Rune) 10) + StringFromRunes(model.GetLine(row));
			line = model.GetLine(maxrow);
			res = res + ustring.Make((Rune) 10) + StringFromRunes(line.GetRange(0, endCol));
			return res;
		}

		//
		// Clears the contents of the selected region
		//
		void ClearRegion()
		{
			long start, end;
			var currentEncoded = ((long) (uint) CurrentRow << 32) | (uint) CurrentColumn;
			GetEncodedRegionBounds(out start, out end);
			var startRow = (int) (start >> 32);
			var maxrow = (int) (end >> 32);
			var startCol = (int) (start & 0xffffffff);
			var endCol = (int) (end & 0xffffffff);
			var line = model.GetLine(startRow);

			if (startRow == maxrow) {
				line.RemoveRange(startCol, endCol - startCol);
				CurrentColumn = startCol;
				SetNeedsDisplay(new Rect(0, startRow - topRow, Frame.Width, startRow - topRow + 1));
				return;
			}

			line.RemoveRange(startCol, line.Count - startCol);
			var line2 = model.GetLine(maxrow);
			line.AddRange(line2.Skip(endCol));
			for (var row = startRow + 1; row <= maxrow; row++) model.RemoveLine(startRow + 1);
			if (currentEncoded == end) CurrentRow -= maxrow - startRow;
			CurrentColumn = startCol;

			SetNeedsDisplay();
		}

		/// <summary>
		///     Redraw the text editor region
		/// </summary>
		/// <param name="region">The region to redraw.</param>
		public override void Redraw(Rect region)
		{
			ColorNormal();

			var bottom = region.Bottom;
			var right = region.Right;
			for (var row = region.Top; row < bottom; row++) {
				var textLine = topRow + row;
				if (textLine >= model.Count) {
					ColorNormal();
					ClearRegion(region.Left, row, region.Right, row + 1);
					continue;
				}

				var line = model.GetLine(textLine);
				var lineRuneCount = line.Count;
				if (line.Count < region.Left) {
					ClearRegion(region.Left, row, region.Right, row + 1);
					continue;
				}

				Move(region.Left, row);
				for (var col = region.Left; col < right; col++) {
					var lineCol = leftColumn + col;
					var rune = lineCol >= lineRuneCount ? ' ' : line[lineCol];
					if (selecting && PointInSelection(col, row))
						ColorSelection();
					else
						ColorNormal();

					AddRune(col, row, rune);
				}
			}

			PositionCursor();
		}

		public override bool CanFocus {
			get => true;
			set => base.CanFocus = value;
		}

		void SetClipboard(ustring text)
		{
			Clipboard.Contents = text;
		}

		void AppendClipboard(ustring text)
		{
			Clipboard.Contents = Clipboard.Contents + text;
		}

		void Insert(Rune rune)
		{
			var line = GetCurrentLine();
			line.Insert(CurrentColumn, rune);
			var prow = CurrentRow - topRow;

			SetNeedsDisplay(new Rect(0, prow, Frame.Width, prow + 1));
		}

		ustring StringFromRunes(List<Rune> runes)
		{
			if (runes == null)
				throw new ArgumentNullException(nameof(runes));
			var size = 0;
			foreach (var rune in runes) size += Utf8.RuneLen(rune);
			var encoded = new byte [size];
			var offset = 0;
			foreach (var rune in runes) offset += Utf8.EncodeRune(rune, encoded, offset);
			return ustring.Make(encoded);
		}

		List<Rune> GetCurrentLine()
		{
			return model.GetLine(CurrentRow);
		}

		void InsertText(ustring text)
		{
			var lines = TextModel.StringToRunes(text);

			if (lines.Count == 0)
				return;

			var line = GetCurrentLine();

			// Optmize single line
			if (lines.Count == 1) {
				line.InsertRange(CurrentColumn, lines[0]);
				CurrentColumn += lines[0].Count;
				if (CurrentColumn - leftColumn > Frame.Width)
					leftColumn = CurrentColumn - Frame.Width + 1;
				SetNeedsDisplay(new Rect(0, CurrentRow - topRow, Frame.Width, CurrentRow - topRow + 1));
				return;
			}

			// Keep a copy of the rest of the line
			var restCount = line.Count - CurrentColumn;
			var rest = line.GetRange(CurrentColumn, restCount);
			line.RemoveRange(CurrentColumn, restCount);

			// First line is inserted at the current location, the rest is appended
			line.InsertRange(CurrentColumn, lines[0]);

			for (var i = 1; i < lines.Count; i++)
				model.AddLine(CurrentRow + i, lines[i]);

			var last = model.GetLine(CurrentRow + lines.Count - 1);
			var lastp = last.Count;
			last.InsertRange(last.Count, rest);

			// Now adjjust column and row positions
			CurrentRow += lines.Count - 1;
			CurrentColumn = lastp;
			if (CurrentRow - topRow > Frame.Height) {
				topRow = CurrentRow - Frame.Height + 1;
				if (topRow < 0)
					topRow = 0;
			}

			if (CurrentColumn < leftColumn)
				leftColumn = CurrentColumn;
			if (CurrentColumn - leftColumn >= Frame.Width)
				leftColumn = CurrentColumn - Frame.Width + 1;
			SetNeedsDisplay();
		}

		// The column we are tracking, or -1 if we are not tracking any column
		int columnTrack = -1;

		// Tries to snap the cursor to the tracking column
		void TrackColumn()
		{
			// Now track the column
			var line = GetCurrentLine();
			if (line.Count < columnTrack)
				CurrentColumn = line.Count;
			else if (columnTrack != -1)
				CurrentColumn = columnTrack;
			else if (CurrentColumn > line.Count)
				CurrentColumn = line.Count;
			Adjust();
		}

		void Adjust()
		{
			var need = false;
			if (CurrentColumn < leftColumn) {
				CurrentColumn = leftColumn;
				need = true;
			}

			if (CurrentColumn - leftColumn > Frame.Width) {
				leftColumn = CurrentColumn - Frame.Width + 1;
				need = true;
			}

			if (CurrentRow < topRow) {
				topRow = CurrentRow;
				need = true;
			}

			if (CurrentRow - topRow > Frame.Height) {
				topRow = CurrentRow - Frame.Height + 1;
				need = true;
			}

			if (need)
				SetNeedsDisplay();
			else
				PositionCursor();
		}

		bool lastWasKill;

		public override bool ProcessKey(KeyEvent kb)
		{
			int restCount;
			List<Rune> rest;

			// Handle some state here - whether the last command was a kill
			// operation and the column tracking (up/down)
			switch (kb.Key) {
			case Key.ControlN:
			case Key.CursorDown:
			case Key.ControlP:
			case Key.CursorUp:
				lastWasKill = false;
				break;
			case Key.ControlK:
				break;
			default:
				lastWasKill = false;
				columnTrack = -1;
				break;
			}

			// Dispatch the command.
			switch (kb.Key) {
			case Key.PageDown:
			case Key.ControlV:
				var nPageDnShift = Frame.Height - 1;
				if (CurrentRow < model.Count) {
					if (columnTrack == -1)
						columnTrack = CurrentColumn;
					CurrentRow = CurrentRow + nPageDnShift > model.Count ? model.Count : CurrentRow + nPageDnShift;
					if (topRow < CurrentRow - nPageDnShift) {
						topRow = CurrentRow >= model.Count ? CurrentRow - nPageDnShift : topRow + nPageDnShift;
						SetNeedsDisplay();
					}

					TrackColumn();
					PositionCursor();
				}

				break;

			case Key.PageUp:
			case 'v' + Key.AltMask:
				var nPageUpShift = Frame.Height - 1;
				if (CurrentRow > 0) {
					if (columnTrack == -1)
						columnTrack = CurrentColumn;
					CurrentRow = CurrentRow - nPageUpShift < 0 ? 0 : CurrentRow - nPageUpShift;
					if (CurrentRow < topRow) {
						topRow = topRow - nPageUpShift < 0 ? 0 : topRow - nPageUpShift;
						SetNeedsDisplay();
					}

					TrackColumn();
					PositionCursor();
				}

				break;

			case Key.ControlN:
			case Key.CursorDown:
				if (CurrentRow + 1 < model.Count) {
					if (columnTrack == -1)
						columnTrack = CurrentColumn;
					CurrentRow++;
					if (CurrentRow >= topRow + Frame.Height) {
						topRow++;
						SetNeedsDisplay();
					}

					TrackColumn();
					PositionCursor();
				}

				break;

			case Key.ControlP:
			case Key.CursorUp:
				if (CurrentRow > 0) {
					if (columnTrack == -1)
						columnTrack = CurrentColumn;
					CurrentRow--;
					if (CurrentRow < topRow) {
						topRow--;
						SetNeedsDisplay();
					}

					TrackColumn();
					PositionCursor();
				}

				break;

			case Key.ControlF:
			case Key.CursorRight:
				var currentLine = GetCurrentLine();
				if (CurrentColumn < currentLine.Count) {
					CurrentColumn++;
					if (CurrentColumn >= leftColumn + Frame.Width) {
						leftColumn++;
						SetNeedsDisplay();
					}

					PositionCursor();
				} else {
					if (CurrentRow + 1 < model.Count) {
						CurrentRow++;
						CurrentColumn = 0;
						leftColumn = 0;
						if (CurrentRow >= topRow + Frame.Height) topRow++;
						SetNeedsDisplay();
						PositionCursor();
					}
				}

				break;

			case Key.ControlB:
			case Key.CursorLeft:
				if (CurrentColumn > 0) {
					CurrentColumn--;
					if (CurrentColumn < leftColumn) {
						leftColumn--;
						SetNeedsDisplay();
					}

					PositionCursor();
				} else {
					if (CurrentRow > 0) {
						CurrentRow--;
						if (CurrentRow < topRow) topRow--;
						currentLine = GetCurrentLine();
						CurrentColumn = currentLine.Count;
						var prev = leftColumn;
						leftColumn = CurrentColumn - Frame.Width + 1;
						if (leftColumn < 0)
							leftColumn = 0;
						if (prev != leftColumn)
							SetNeedsDisplay();
						PositionCursor();
					}
				}

				break;

			case Key.Delete:
			case Key.Backspace:
				if (ReadOnly)
					break;
				if (CurrentColumn > 0) {
					// Delete backwards 
					currentLine = GetCurrentLine();
					currentLine.RemoveAt(CurrentColumn - 1);
					CurrentColumn--;
					if (CurrentColumn < leftColumn) {
						leftColumn--;
						SetNeedsDisplay();
					} else
						SetNeedsDisplay(new Rect(0, CurrentRow - topRow, 1, Frame.Width));
				} else {
					// Merges the current line with the previous one.
					if (CurrentRow == 0)
						return true;
					var prowIdx = CurrentRow - 1;
					var prevRow = model.GetLine(prowIdx);
					var prevCount = prevRow.Count;
					model.GetLine(prowIdx).AddRange(GetCurrentLine());
					model.RemoveLine(CurrentRow);
					CurrentRow--;
					CurrentColumn = prevCount;
					leftColumn = CurrentColumn - Frame.Width + 1;
					if (leftColumn < 0)
						leftColumn = 0;
					SetNeedsDisplay();
				}

				break;

			// Home, C-A
			case Key.Home:
			case Key.ControlA:
				CurrentColumn = 0;
				if (CurrentColumn < leftColumn) {
					leftColumn = 0;
					SetNeedsDisplay();
				} else
					PositionCursor();

				break;
			case Key.DeleteChar:
			case Key.ControlD: // Delete
				if (ReadOnly)
					break;
				currentLine = GetCurrentLine();
				if (CurrentColumn == currentLine.Count) {
					if (CurrentRow + 1 == model.Count)
						break;
					var nextLine = model.GetLine(CurrentRow + 1);
					currentLine.AddRange(nextLine);
					model.RemoveLine(CurrentRow + 1);
					var sr = CurrentRow - topRow;
					SetNeedsDisplay(new Rect(0, sr, Frame.Width, sr + 1));
				} else {
					currentLine.RemoveAt(CurrentColumn);
					var r = CurrentRow - topRow;
					SetNeedsDisplay(new Rect(CurrentColumn - leftColumn, r, Frame.Width, r + 1));
				}

				break;

			case Key.End:
			case Key.ControlE: // End
				currentLine = GetCurrentLine();
				CurrentColumn = currentLine.Count;
				var pcol = leftColumn;
				leftColumn = CurrentColumn - Frame.Width + 1;
				if (leftColumn < 0)
					leftColumn = 0;
				if (pcol != leftColumn)
					SetNeedsDisplay();
				PositionCursor();
				break;

			case Key.ControlK: // kill-to-end
				if (ReadOnly)
					break;
				currentLine = GetCurrentLine();
				if (currentLine.Count == 0) {
					model.RemoveLine(CurrentRow);
					var val = ustring.Make((Rune) '\n');
					if (lastWasKill)
						AppendClipboard(val);
					else
						SetClipboard(val);
				} else {
					restCount = currentLine.Count - CurrentColumn;
					rest = currentLine.GetRange(CurrentColumn, restCount);
					var val = StringFromRunes(rest);
					if (lastWasKill)
						AppendClipboard(val);
					else
						SetClipboard(val);
					currentLine.RemoveRange(CurrentColumn, restCount);
				}

				SetNeedsDisplay(new Rect(0, CurrentRow - topRow, Frame.Width, Frame.Height));
				lastWasKill = true;
				break;

			case Key.ControlY: // Control-y, yank
				if (ReadOnly)
					break;
				InsertText(Clipboard.Contents);
				selecting = false;
				break;

			case Key.ControlSpace:
				selecting = true;
				selectionStartColumn = CurrentColumn;
				selectionStartRow = CurrentRow;
				break;

			case 'w' + Key.AltMask:
				SetClipboard(GetRegion());
				selecting = false;
				break;

			case Key.ControlW:
				SetClipboard(GetRegion());
				if (!ReadOnly)
					ClearRegion();
				selecting = false;
				break;

			case 'b' + Key.AltMask:
				var newPos = WordBackward(CurrentColumn, CurrentRow);
				if (newPos.HasValue) {
					CurrentColumn = newPos.Value.col;
					CurrentRow = newPos.Value.row;
				}

				Adjust();

				break;

			case 'f' + Key.AltMask:
				newPos = WordForward(CurrentColumn, CurrentRow);
				if (newPos.HasValue) {
					CurrentColumn = newPos.Value.col;
					CurrentRow = newPos.Value.row;
				}

				Adjust();
				break;

			case Key.Enter:
				if (ReadOnly)
					break;
				var orow = CurrentRow;
				currentLine = GetCurrentLine();
				restCount = currentLine.Count - CurrentColumn;
				rest = currentLine.GetRange(CurrentColumn, restCount);
				currentLine.RemoveRange(CurrentColumn, restCount);
				model.AddLine(CurrentRow + 1, rest);
				CurrentRow++;
				var fullNeedsDisplay = false;
				if (CurrentRow >= topRow + Frame.Height) {
					topRow++;
					fullNeedsDisplay = true;
				}

				CurrentColumn = 0;
				if (CurrentColumn < leftColumn) {
					fullNeedsDisplay = true;
					leftColumn = 0;
				}

				if (fullNeedsDisplay)
					SetNeedsDisplay();
				else
					SetNeedsDisplay(new Rect(0, CurrentRow - topRow, 0, Frame.Height));
				break;

			default:
				// Ignore control characters and other special keys
				if (kb.Key < Key.Space || kb.Key > Key.CharMask)
					return false;
				//So that special keys like tab can be processed
				if (ReadOnly)
					return true;
				Insert((uint) kb.Key);
				CurrentColumn++;
				if (CurrentColumn >= leftColumn + Frame.Width) {
					leftColumn++;
					SetNeedsDisplay();
				}

				PositionCursor();
				return true;
			}

			return true;
		}

		IEnumerable<(int col, int row, Rune rune)> ForwardIterator(int col, int row)
		{
			if (col < 0 || row < 0)
				yield break;
			if (row >= model.Count)
				yield break;
			var line = GetCurrentLine();
			if (col >= line.Count)
				yield break;

			while (row < model.Count) {
				for (var c = col; c < line.Count; c++) yield return (c, row, line[c]);
				col = 0;
				row++;
				line = GetCurrentLine();
			}
		}

		Rune RuneAt(int col, int row)
		{
			return model.GetLine(row)[col];
		}

		bool MoveNext(ref int col, ref int row, out Rune rune)
		{
			var line = model.GetLine(row);
			if (col + 1 < line.Count) {
				col++;
				rune = line[col];
				return true;
			}

			while (row + 1 < model.Count) {
				col = 0;
				row++;
				line = model.GetLine(row);
				if (line.Count > 0) {
					rune = line[0];
					return true;
				}
			}

			rune = 0;
			return false;
		}

		bool MovePrev(ref int col, ref int row, out Rune rune)
		{
			var line = model.GetLine(row);

			if (col > 0) {
				col--;
				rune = line[col];
				return true;
			}

			if (row == 0) {
				rune = 0;
				return false;
			}

			while (row > 0) {
				row--;
				line = model.GetLine(row);
				col = line.Count - 1;
				if (col >= 0) {
					rune = line[col];
					return true;
				}
			}

			rune = 0;
			return false;
		}

		(int col, int row)? WordForward(int fromCol, int fromRow)
		{
			var col = fromCol;
			var row = fromRow;
			var line = GetCurrentLine();
			var rune = RuneAt(col, row);

			var srow = row;
			if (Rune.IsPunctuation(rune) || Rune.IsWhiteSpace(rune)) {
				while (MoveNext(ref col, ref row, out rune))
					if (Rune.IsLetterOrDigit(rune))
						break;
				while (MoveNext(ref col, ref row, out rune))
					if (!Rune.IsLetterOrDigit(rune))
						break;
			} else
				while (MoveNext(ref col, ref row, out rune))
					if (!Rune.IsLetterOrDigit(rune))
						break;

			if (fromCol != col || fromRow != row)
				return (col, row);
			return null;
		}

		(int col, int row)? WordBackward(int fromCol, int fromRow)
		{
			if (fromRow == 0 && fromCol == 0)
				return null;

			var col = fromCol;
			var row = fromRow;
			var line = GetCurrentLine();
			var rune = RuneAt(col, row);

			if (Rune.IsPunctuation(rune) || Rune.IsSymbol(rune) || Rune.IsWhiteSpace(rune)) {
				while (MovePrev(ref col, ref row, out rune))
					if (Rune.IsLetterOrDigit(rune))
						break;
				while (MovePrev(ref col, ref row, out rune))
					if (!Rune.IsLetterOrDigit(rune))
						break;
			} else
				while (MovePrev(ref col, ref row, out rune))
					if (!Rune.IsLetterOrDigit(rune))
						break;

			if (fromCol != col || fromRow != row)
				return (col, row);
			return null;
		}

		public override bool MouseEvent(MouseEvent ev)
		{
			if (!ev.Flags.HasFlag(MouseFlags.Button1Clicked)) return false;

			if (!HasFocus)
				SuperView.SetFocus(this);


			var maxCursorPositionableLine = model.Count - 1 - topRow;
			if (ev.Y > maxCursorPositionableLine)
				CurrentRow = maxCursorPositionableLine;
			else
				CurrentRow = ev.Y + topRow;
			var r = GetCurrentLine();
			if (ev.X - leftColumn >= r.Count)
				CurrentColumn = r.Count - leftColumn;
			else
				CurrentColumn = ev.X - leftColumn;

			PositionCursor();
			return true;
		}
	}
}