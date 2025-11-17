package org.example

import org.knowm.xchart.XYChartBuilder
import org.knowm.xchart.SwingWrapper

fun main() {
    val quorumValues = listOf(1, 2, 3, 4, 5)
    val avgLatency = listOf(5.2, 7.1, 10.5, 14.3, 19.8)

    val chart = XYChartBuilder()
        .width(800)
        .height(600)
        .title("Quorum vs Latency")
        .xAxisTitle("Write Quorum")
        .yAxisTitle("Average Latency (ms)")
        .build()

    chart.addSeries("Latency", quorumValues, avgLatency)

    SwingWrapper(chart).displayChart()
}
