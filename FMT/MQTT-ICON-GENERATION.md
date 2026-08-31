# MQTT icon generation attempts

Mode: built-in image_gen (imagegen skill), not CLI/API fallback.

Both generated outputs contained an opaque checkerboard (RGB PNG, alpha=255), not actual transparency, and were rejected.

## Final delivery: user-approved local background removal

The user explicitly approved local image processing after the failed transparency attempts. No further image-generation/API call was made. `Make-MqttTransparentIcon.ps1` extracts the blue clouds/digits and white MQTT wordmark from the original `Assets/mqtt-social-720.jpg`, with a narrow anti-aliased alpha edge and tight framing. The original JPEG is unchanged.

Final asset: `FMT/Assets/mqtt-toolbar-transparent.png` (452x353 RGBA PNG). The toolbar loads this embedded PNG with transparent background and aspect-preserving scaling. The generated checkerboard drafts are not shipped.

Executable: `bin/MqttTransparentIconPreview/FMTPlanner.exe`. Includes the SiK layout correction and MQTT traffic indicator.

## First prompt

Use case: background-extraction. Asset type: small desktop toolbar MQTT icon, shown around 42 x 28 pixels on a dark background. Image 1 is the edit target. Remove the entire gray photographic background and its cast shadows, replacing it with genuine transparent alpha (PNG), including the spaces around the clouds, between the digits, and inside the letters. Keep the overlapping cyan/blue clouds and the white bold text exactly "MQTT" (M Q T T), with the blue binary digits between them. Keep their original shapes, relative positions, colors and clean edge details. Tight landscape framing around the complete cloud-and-wordmark composition with only a very small transparent padding, about a 3:2 aspect ratio. No clipped letters, no shadow halo, no gray/white rectangular backdrop, no checkerboard baked into the image, no extra elements or words. Clean readable small-size icon, retaining the supplied design.

## Final prompt (second attempt)

Create a transparent-background PNG UI icon with ACTUAL alpha channel, not RGB. The reference is a design reference for a new simplified MQTT toolbar icon. Keep two overlapping blue and cyan clouds above the exact white bold uppercase word "MQTT". Simplify away the binary digits for clarity at 42x28 pixels. Tight 3:2 landscape composition, minimal padding, entire icon visible. Export a real RGBA PNG: all empty pixels around the clouds and typography, and inside the Q, must be zero-alpha transparent. NO checkerboard drawn into the pixels, NO solid background, NO gradient background, NO mockup, NO shadows, NO watermark. Crisp clean silhouette and white MQTT lettering readable on a dark UI.
