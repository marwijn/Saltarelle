﻿using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Saltarelle.Fragments {
	internal class LiteralFragment : IFragment {
		public readonly string Text;
		public readonly bool IsCData;

		public IFragment TryMergeWithNext(IFragment nextFragment) {
			LiteralFragment lf = nextFragment as LiteralFragment;
			if (lf == null || lf.IsCData || IsCData)
				return null;
			if (lf.Text == "")
				return this;
			if (Text == "")
				return lf;
			if (Text.EndsWith(" ") && lf.Text.StartsWith(" ")) // Collapse spaces
				return new LiteralFragment(Text.Substring(0, Text.Length - 1) + lf.Text);
			return new LiteralFragment(Text + lf.Text);
		}

		public void Render(ITemplate tpl, IInstantiatedTemplateControl ctl, StringBuilder sb) {
			if (IsCData)
				sb.Append("<![CDATA[");
			sb.Append(Text);
			if (IsCData)
				sb.Append("]]>");
		}

#if SERVER
		public LiteralFragment(string text) : this(text, false) {
		}

		public LiteralFragment(string text, bool isCData) {
			if (text == null) throw Utils.ArgumentNullException("text");
			this.Text = text;
			this.IsCData = isCData;
		}

		public override bool Equals(object obj) {
			var other = obj as LiteralFragment;
			if (other == null)
				return false;
			return Text == other.Text && IsCData == other.IsCData;
		}

		public override int GetHashCode() {
			return Text.GetHashCode() ^ IsCData.GetHashCode();
		}

		public override string ToString() {
			return IsCData ? "<![CDATA[" + Text + "]]>" : Text;
		}

		public void WriteCode(ITemplate tpl, FragmentCodePoint point, CodeBuilder cb) {
			cb.AppendLine(ParserUtils.RenderFunctionStringBuilderName + ".Append(@\"" + (IsCData ? "<![CDATA[" : "") + Text.Replace("\"", "\"\"") + (IsCData ? "]]>" : "") + "\");");
		}
#endif

#if CLIENT
		[AlternateSignature]
		public LiteralFragment(string text) {}
		public LiteralFragment(string text, bool isCData) {
			if (text == null) throw Utils.ArgumentNullException("text");
			this.Text = text;
			this.IsCData = (bool)((object)isCData ?? false);
		}
#endif
	}
}
