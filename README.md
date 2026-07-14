# Image Printer

Converts images to ASCII text. Shared product name and actions across apps: **Open image**, **Save as text**, **Open text file**, **Copy text**, **Save as PDF**, **Text to image**, **Open video**, **Resolution** (1–100%), and **Invert grayscale**.

## Image Printer (library)

Core conversion library: palettes, documents, image rebuild (UnText), frame batch conversion, and single-page PDF export.

## Image Printer.Video

OpenCV-backed video frame extraction used by WinUI, WPF GUI, and the Video Printer CLI.

## Image Printer CLI

Console converter: open an image path, set resolution and invert grayscale, choose an ASCII set, and write `.txt` output (including GIF frame export).

## Image Printer GUI

WPF desktop UI with full feature set: text/PDF export, text-to-image, video frames, ASCII editor.

## Image Printer WinUI

Windows App SDK / WinUI 3 Store app with the same feature set as the WPF GUI.

## UnText Filer

CLI fallback: rebuild a grayscale image from an ASCII `.txt` file (uses the shared library).

## Video Printer

CLI fallback: extract video frames and convert them to ASCII (uses Image Printer.Video).

## Image Resizer

Uses `ImagePrinter` to resize images.
