@echo off
REM installs all Helm charts

helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo add datalust https://helm.datalust.co
helm repo add bitnami https://charts.bitnami.com/bitnami
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update