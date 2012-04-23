﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Composite.Core.IO;
using Composite.Core.Xml;

namespace Composite.Core.PackageSystem.PackageFragmentInstallers
{
	/// <summary>    
	/// </summary>
	/// <exclude />
	[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)] 
	public class TransformXmlPackageFragmentUninstaller : BasePackageFragmentUninstaller
	{
		private sealed class XmlFile
		{
			public string Source { get; set; }
			public string Target { get; set; }
		}

		private IList<XmlFile> _xmlFiles;

		/// <exclude />
		public override void Uninstall()
		{
			if (_xmlFiles == null) throw new InvalidOperationException("MergeXmlPackageFragmentUninstaller has not been validated");

			foreach (XmlFile xmlFile in _xmlFiles)
			{
				string targetXml = PathUtil.Resolve(xmlFile.Target);

				using (Stream stream = this.UninstallerContext.ZipFileSystem.GetFileStream(xmlFile.Source))
				{
					XElement source = XElement.Load(stream);
					XDocument target = XDocument.Load(targetXml);

					target.Root.Exclude(source);
					target.SaveToFile(targetXml);
				}
			}
		}

		/// <exclude />
		public override IEnumerable<PackageFragmentValidationResult> Validate()
		{
			List<PackageFragmentValidationResult> validationResult = new List<PackageFragmentValidationResult>();

			if (Configuration.Count(f => f.Name == "XmlFiles") > 1)
			{
				validationResult.Add(new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, "OnlyOneFilesElement"));

				return validationResult;
			}

			IEnumerable<XElement> filesElement = this.Configuration.Where(f => f.Name == "XmlFiles");

			_xmlFiles = new List<XmlFile>();

			foreach (var fileElement in filesElement.Elements("XmlFile"))
			{
				XAttribute sourceAttribute = fileElement.Attribute("source");
				XAttribute targetAttribute = fileElement.Attribute("target");

				if (sourceAttribute == null || targetAttribute == null)
				{
					validationResult.Add(new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, "MissingAttribute", fileElement));

					continue;
				}

				XmlFile xmlFile = new XmlFile
				{
					Source = sourceAttribute.Value,
					Target = targetAttribute.Value
				};

				if (!C1File.Exists(PathUtil.Resolve(xmlFile.Target)))
				{
					validationResult.Add(new PackageFragmentValidationResult(PackageFragmentValidationResultType.Fatal, "FileNotFound", fileElement));

					continue;
				}

				_xmlFiles.Add(xmlFile);
			}

			if (validationResult.Count > 0)
			{
				_xmlFiles = null;
			}

			return validationResult;
		}
	}
}
