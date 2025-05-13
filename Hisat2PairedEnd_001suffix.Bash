#!/bin/bash
set -x
# Define input/output paths
INDEX="hisat2_index/grch38_snp_rep/genome_snp_rep"
FASTQ_DIR="fastq"
OUT_DIR="hisat2_alignments/paired-end"

shopt -s nullglob

# Make output dir if it doesn’t exist
mkdir -p "$OUT_DIR"

# Loop through R1 files
for r1 in fastq/*R1_001.fastq.gz; do
    # Derive the R2 filename
    r2="${r1/R1_001.fastq.gz/R2_001.fastq.gz}"

    # Extract sample name (removing -R1_fastq-data.fastq.gz)
    sample=$(basename "$r1" R1_001.fastq.gz)

    echo "Aligning paired-end reads for $sample..."

    # Run HISAT2 for paired-end
    hisat2 -p 8 -x "$INDEX" -1 "$r1" -2 "$r2" -S "$OUT_DIR/${sample}.sam"
done