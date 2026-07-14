# Image Printer

Converts images to ASCII text. Shared product name and actions across every app: **Open image**, **Save as text**, **Open text file**, **Copy text**, **Resolution** (1–100%), and **Invert grayscale**.

## Image Printer (library)

Core conversion library used by the other apps.

## Image Printer CLI

Console converter: open an image path, set resolution and invert grayscale, choose an ASCII set, and write `.txt` output (including GIF frame export).

## Image Printer GUI

WPF desktop UI with open/save/copy, resolution, invert grayscale, and an ASCII character-set editor.

## Image Printer WinUI

Windows App SDK / WinUI 3 app intended for the Microsoft Store. Same features as the WPF GUI: open image, preview, image/export paths, resolution, invert grayscale, ASCII character editor and set picker, save as text, copy text, and open the saved text file.

## Image Resizer

Uses `ImagePrinter` to resize images.

## UnText Filer

Converts ASCII text back to a grayscale image (assumes the default ASCII set).

## Video Printer

Prints video frames over time.
