// SPDX-License-Identifier: MIT
// Velyntora Remove AI - first Pinta-native port of the previous Krita scaffold.
// MI-GAN inference is intentionally isolated behind IRemoveAiBackend so the
// UI/tool does not depend on a particular native runtime.

using System;
using Pinta.Core;

namespace Pinta.Tools;

internal readonly record struct RemoveAiRequest (
	Cairo.ImageSurface Source,
	Cairo.ImageSurface Mask,
	int ModelSize = 512,
	bool ModelHandlesPipeline = true);

internal readonly record struct RemoveAiResult (
	bool Ok,
	Cairo.ImageSurface? Image,
	Gdk.Rectangle WriteBackRect,
	long SelectedPixelCount,
	string? Error)
{
	public bool IsReadyForCommit =>
		Ok &&
		Image is not null &&
		SelectedPixelCount > 0 &&
		WriteBackRect.Width > 0 &&
		WriteBackRect.Height > 0 &&
		string.IsNullOrEmpty (Error);
}

internal interface IRemoveAiBackend
{
	bool IsAvailable { get; }
	RemoveAiResult Run (RemoveAiRequest request);
}

/// <summary>
/// MI-GAN backend boundary for Velyntora Remove AI.
///
/// The old Krita implementation already defined the important contract:
/// source image + binary mask -> local MI-GAN/ONNX inference -> validated
/// write-back.  Pinta is C#, so the old Qt/C++ implementation cannot simply
/// be copied.  This class preserves that contract while the Android/native
/// ONNX Runtime adapter is connected.
/// </summary>
internal sealed class MiganRemoveAiBackend : IRemoveAiBackend
{
	public const int DefaultModelSize = 512;
	public const string ModelFileName = "migan.onnx";

	// Do not report success until a real local runtime and model are connected.
	public bool IsAvailable => false;

	public RemoveAiResult Run (RemoveAiRequest request)
	{
		if (request.Source is null)
			return Failure ("Remove AI requires a source image.");

		if (request.Mask is null)
			return Failure ("Remove AI requires a mask.");

		if (request.Source.Width != request.Mask.Width ||
		    request.Source.Height != request.Mask.Height)
			return Failure ("Remove AI source and mask dimensions must match.");

		if (!IsAvailable)
			return Failure ("MI-GAN local runtime is not connected yet.");

		// The native adapter will implement the same validated MI-GAN contract
		// used by the previous Velyntora/Krita scaffold:
		//   uint8 image [1,3,H,W] named image
		//   uint8 mask  [1,1,H,W] named mask
		//   uint8 result [1,3,H,W] named result
		// No network/API call belongs here.
		return Failure ("MI-GAN inference adapter is not connected yet.");
	}

	private static RemoveAiResult Failure (string error) =>
		new (
			Ok: false,
			Image: null,
			WriteBackRect: new Gdk.Rectangle (),
			SelectedPixelCount: 0,
			Error: error);
}
