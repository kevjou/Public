#!/bin/bash

# Define input/output paths
INDEX="hisat2_index/GRCh38"
FASTQ_DIR="fastq_temp1"
OUT_DIR="hisat2_alignments"

# Make output dir if it doesn’t exist
mkdir -p "$OUT_DIR"

# Use find to handle spaces in filenames properly
for fq in "$fastq_temp1"/*.fastq.gz; do
    sample=$(basename "$fq" .fastq.gz)
    echo "Aligning $sample..."

    hisat2 -p 8 -x "$INDEX" -U "$fq" -S "$OUT_DIR/${sample}.sam"
done