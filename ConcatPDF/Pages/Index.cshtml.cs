using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mime;

namespace ConcatPDF.Pages
{
	public class IndexModel : PageModel
	{
		private readonly ILogger<IndexModel> _logger;

		public IndexModel(ILogger<IndexModel> logger)
		{
			_logger = logger;
			ProcessingErrors = new List<string>();
		}

		[BindProperty]
		public BufferedMultipleFileUpload FileUpload { get; set; }

		public List<string> ProcessingErrors {get; private set;}

		public void OnGet()
		{
			_logger.Log(LogLevel.Information, "Display combiner");
		}

		public async Task<IActionResult> OnPostUploadAsync()
		{
			if (null == FileUpload.PDFFiles || !ModelState.IsValid)
			{
				_logger.LogError("PDF files missing");
				ProcessingErrors.Add("PDF datoteke nisu odabrane!");
				return Page();
			}

			var combinedPDF = new PdfSharpCore.Pdf.PdfDocument();

			// Process uploaded files
			// Don't rely on or trust the FileName property without validation.
			var sortedPDFs = FileUpload.PDFFiles.OrderBy(pdf => pdf.FileName);
			foreach (var pdf in sortedPDFs)
			{
				if (0 == pdf.Length)
				{
					_logger.LogError("PDF {0} empty", pdf.FileName);
					ProcessingErrors.Add(string.Format("PDF datoteka \"{0}\" je prazna!", pdf.FileName));
					continue;
				}

				try
				{
					// copy pages to combined document
					var doc = PdfSharpCore.Pdf.IO.PdfReader.Open(
						pdf.OpenReadStream(),
						PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import
					);
					_logger.LogInformation("Adding file: {0}, pages: {1}", pdf.FileName, doc.PageCount);
					bool outlineAdded = false;
					foreach (var p in doc.Pages)
					{
						var addedPage = combinedPDF.AddPage(p);
						// add document file name to destination outline (table of content)
						if (outlineAdded) { continue; }
						combinedPDF.Outlines.Add(
							pdf.FileName
							,addedPage, false
						);
						outlineAdded = true;
					}
				}
				catch (System.Exception ex)
				{
					ProcessingErrors.Add(string.Format("PDF datoteka \"{0}\" je nesipravna!", pdf.FileName));
					_logger.LogError(ex, "Error processing {0}", pdf.FileName);
				}

				//var filePath = Path.GetTempFileName();
				//using (var stream = System.IO.File.Create(filePath))
				//{
				//	await formFile.CopyToAsync(stream);
				//}
			}

			// Exit on errors
			if (ProcessingErrors.Count > 0)
			{ return Page(); }

			// download combined file
			MemoryStream ms = new MemoryStream();
			combinedPDF.Save(ms);
			return File(
				ms, 
				MediaTypeNames.Application.Pdf,
				WebUtility.HtmlEncode("combined.pdf")
			);
			//return RedirectToPage("./Index");
		}
	}

	/// <summary>
	/// Model for form file upload, represents form on page
	/// </summary>
	public class BufferedMultipleFileUpload
	{
		[Required]
		[Display(Name = "PDF datoteke za spajanje")]
		public List<IFormFile>? PDFFiles { get; set; }

		//[Display(Name = "Note")]
		//[StringLength(50, MinimumLength = 0)]
		//public string Note { get; set; }
	}
}