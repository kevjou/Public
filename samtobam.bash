#!/bin/bash
set -x
set -o pipefail
shopt -s nullglob

SAM_Directory="hisat2_alignments/paired-end"
TMP_DIR="hisat2_alignments/tmp"
Merged_BAM="$SAM_Directory/merged.bam"

mkdir -p "$TMP_DIR"
#for improved compatability in shell scripts
final_bams=()
sorted_bams=()

# Convert and sort each SAM file
for sam_file in "$SAM_Directory"/*.sam; do
	sample=$(basename "$sam_file" .sam | sed 's/_$//')
    fixmate_bam="$SAM_Directory/${sample}_fixmate.bam"
    sorted_bam="$SAM_Directory/${sample}_sorted.bam"
    markdup_bam="$SAM_Directory/${sample}_markdup.bam"

    echo "Processing $sample..."

	# Step 1: Fix mate information
	samtools fixmate -O bam -m "$SAM_Directory/${sample}_.sam" "$fixmate_bam"
    # Step 2: Sort by genomic position (required for markdup)
    samtools sort -l 1 -@8 -o "$sorted_bam" -T "$TMP_DIR/${sample}" "$fixmate_bam"

    # Step 3: Mark duplicates
    samtools markdup -@8 "$sorted_bam" "$markdup_bam"

done
set +x
