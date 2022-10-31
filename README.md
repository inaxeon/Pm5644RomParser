# Philip PM5644 Pattern ROM parser / processor

This project parses the data from the EPROMs within a Philips PM5644 programmed with the 4:3 PAL circle pattern and turns it into a bitmap image for on-screen viewing.

There are two processing steps:

### Convert the contents of the EPROMs into raw bitmaps

The layout of the samples in the EPROMs is complicated and not fully understood. In order to read them a PM5644 was attached to a logic analyser for the purposes of creating a vector table which enabled the ROMs to be interpreted.

Once the EPROMs are sequenced they are converted into raw bitmap images which are provided in this project. The actual contents of the EPROMs are not at this time. They are not needed to run this program.

### Convert the raw bitmaps into a processed image for on-screen viewing.

The PM5644 effectively contains three monochrome bitmaps in its EPROMs.

1) Luminance component
2) R-Y component
3) B-Y component

This amounts to an image rougly in YCbCr format however the exact design parameters of the unit are not known thus there is quite a bit of guesswork in this project.

The final result is the below however note that the understanding of how to decode the colours is presently in its infancy.

![Composite image](https://github.com/inaxeon/Pm5644RomParser/blob/main/Pm5644RomParser/Samples/PM5644_Composite.png)

Note that the circle is not a true circle. Unfortunately that's just the deal with these units. Analogue televisions do not have pixels but instead lines. Philips have only loaded as many samples as they felt were necessary to generate the pattern.

There is no sense in stretching the image either as this will butcher it.

The is a perculiar looking defect in the blue square in the circle. This comes from the R-Y samples and it is verified to be exactly as reproduced from the physical unit by the verification of checksums labelled on the physical chips.
