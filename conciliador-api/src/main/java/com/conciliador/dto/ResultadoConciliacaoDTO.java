package com.conciliador.dto;

public class ResultadoConciliacaoDTO {
    private String dataEfetiva;
    private String bandeira;
    private double valorVendido;
    private double valorExtrato;
    private double diferenca;

    public ResultadoConciliacaoDTO(String dataEfetiva, String bandeira, double valorVendido, double valorExtrato, double diferenca) {
        this.dataEfetiva = dataEfetiva;
        this.bandeira = bandeira;
        this.valorVendido = valorVendido;
        this.valorExtrato = valorExtrato;
        this.diferenca = diferenca;
    }

    // Getters e Setters
    public String getDataEfetiva() { return dataEfetiva; }
    public void setDataEfetiva(String dataEfetiva) { this.dataEfetiva = dataEfetiva; }

    public String getBandeira() { return bandeira; }
    public void setBandeira(String bandeira) { this.bandeira = bandeira; }

    public double getValorVendido() { return valorVendido; }
    public void setValorVendido(double valorVendido) { this.valorVendido = valorVendido; }

    public double getValorExtrato() { return valorExtrato; }
    public void setValorExtrato(double valorExtrato) { this.valorExtrato = valorExtrato; }

    public double getDiferenca() { return diferenca; }
    public void setDiferenca(double diferenca) { this.diferenca = diferenca; }
}