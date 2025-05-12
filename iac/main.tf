terraform {
  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }

  #backend "azurerm" {
  #  storage_account_name = "diginsightanalyzerstg00"
  #  container_name = "tfstates"
  #  key = ""
  #  use_azuread_auth = true
  #}

  required_version = "~> 1.0"
}

variable "suffix" {
  type = string
}

variable "rg" {
  type = object({
    name = string
  })
}

variable "stg" {
  type = object({
    account_tier             = string
    account_replication_type = string
  })
}

locals {
  location  = "switzerlandnorth"
  tenant_id = "16b3c013-d300-468d-ac64-7eda0820b6d3"
}

provider "azuread" {
  tenant_id = local.tenant_id
}

provider "azurerm" {
  tenant_id           = local.tenant_id
  subscription_id     = "e30f5ae8-3ae1-41f1-8ce2-564804131bd6"
  storage_use_azuread = true

  features {}
}

provider "random" {}

data "azuread_group" "contributors" {
  display_name = "diginsight-analyzer-contributors"
}

resource "azuread_application" "main" {
  display_name                 = "diginsight-analyzer"
  service_management_reference = "e4847f9a-c5dc-435d-9d53-9b9c65b154e7"
}

resource "azurerm_resource_group" "main" {
  name     = var.rg.name != null ? var.rg.name : "diginsight-analyzer-rg-${var.suffix}"
  location = local.location
}

resource "random_string" "vnet_link" {
  length      = 13
  lower       = true
  upper       = false
  numeric     = true
  special     = false
  min_lower   = 1
  min_numeric = 1
  keepers = {
    suffix = var.suffix
  }
}

resource "azurerm_virtual_network" "main" {
  name                = "diginsight-analyzer-vnet-${random_string.vnet_link.keepers.suffix}"
  location            = local.location
  resource_group_name = azurerm_resource_group.main.name
  address_space = [
    "10.0.0.0/16"
  ]
}

resource "azurerm_subnet" "infra" {
  name                            = "infra"
  virtual_network_name            = azurerm_virtual_network.main.name
  resource_group_name             = azurerm_resource_group.main.name
  address_prefixes                = ["10.0.0.0/24"]
  default_outbound_access_enabled = false
}

resource "azurerm_network_security_group" "infra" {
  name                = "${azurerm_virtual_network.main.name}-${azurerm_subnet.infra.name}-nsg-${local.location}"
  location            = local.location
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_subnet_network_security_group_association" "infra" {
  subnet_id                 = azurerm_subnet.infra.id
  network_security_group_id = azurerm_network_security_group.infra.id
}

resource "azurerm_subnet" "dns" {
  name                 = "dns"
  virtual_network_name = azurerm_virtual_network.main.name
  resource_group_name  = azurerm_resource_group.main.name
  address_prefixes = [
    "10.0.1.32/28"
  ]

  delegation {
    name = "Microsoft.Network.dnsResolvers"
    service_delegation {
      name = "Microsoft.Network/dnsResolvers"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action"
      ]
    }
  }
}

resource "azurerm_network_security_group" "dns" {
  name                = "${azurerm_virtual_network.main.name}-${azurerm_subnet.dns.name}-nsg-${local.location}"
  location            = local.location
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_subnet_network_security_group_association" "dns" {
  subnet_id                 = azurerm_subnet.dns.id
  network_security_group_id = azurerm_network_security_group.dns.id
}

resource "azurerm_private_dns_resolver" "main" {
  name                = "diginsight-analyzer-dnspr-${var.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  virtual_network_id  = azurerm_virtual_network.main.id
}

resource "azurerm_private_dns_resolver_inbound_endpoint" "main" {
  name                    = azurerm_subnet.dns.name
  location                = local.location
  private_dns_resolver_id = azurerm_private_dns_resolver.main.id

  ip_configurations {
    subnet_id = azurerm_subnet.dns.id
  }
}

resource "azurerm_public_ip" "vngw" {
  name                = "diginsight-analyzer-ip-${var.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  allocation_method   = "Static"
  sku                 = "Standard"
}

resource "azurerm_subnet" "vngw" {
  name                 = "GatewaySubnet"
  virtual_network_name = azurerm_virtual_network.main.name
  resource_group_name  = azurerm_resource_group.main.name
  address_prefixes     = ["10.0.1.0/27"]
}

resource "azurerm_virtual_network_gateway" "main" {
  name                = "diginsight-analyzer-vngw-${var.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  type                = "Vpn"
  sku                 = "VpnGw1"

  ip_configuration {
    name                 = "default"
    public_ip_address_id = azurerm_public_ip.vngw.id
    subnet_id            = azurerm_subnet.vngw.id
  }

  custom_route {
    address_prefixes = ["10.0.0.0/16"]
  }

  vpn_client_configuration {
    address_space = ["10.1.0.0/29"]
    aad_audience  = "c632b3df-fb67-4d84-bdcf-b95ad541b5c8"
    aad_issuer    = "https://sts.windows.net/${local.tenant_id}/"
    aad_tenant    = "https://login.microsoftonline.com/${local.tenant_id}"
  }
}

resource "azurerm_storage_account" "main" {
  name                             = "diginsightanalyzerstg${var.suffix}"
  resource_group_name              = azurerm_resource_group.main.name
  location                         = local.location
  account_tier                     = var.stg.account_tier
  account_replication_type         = var.stg.account_replication_type
  local_user_enabled               = false
  public_network_access_enabled    = false
  shared_access_key_enabled        = false
  allow_nested_items_to_be_public  = false
  cross_tenant_replication_enabled = false
}

# TODO IAM of Storage Account

resource "random_uuid" "stg_nic_ipconfig" {
  keepers = {
    suffix = var.suffix
  }
}

locals {
  stg_pve_name     = "diginsight-analyzer-stg-${random_uuid.stg_nic_ipconfig.keepers.suffix}-pve-01"
  stg_pve_nic_name = "${local.stg_pve_name}-nic"
}

resource "azurerm_network_interface" "stg" {
  name                = local.stg_pve_nic_name
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location

  ip_configuration {
    name                          = "privateEndpointIpConfig.${random_uuid.stg_nic_ipconfig.result}"
    private_ip_address_allocation = "Dynamic"
    subnet_id                     = azurerm_subnet.infra.id
  }
}

resource "azurerm_private_dns_zone" "blob" {
  name                = "privatelink.blob.core.windows.net"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_dns_a_record" "stg" {
  name                = azurerm_storage_account.main.name
  resource_group_name = azurerm_resource_group.main.name
  zone_name           = azurerm_private_dns_zone.blob.name
  ttl                 = 10
  records = [
    azurerm_network_interface.stg.private_ip_address
  ]
}

resource "azurerm_private_dns_zone_virtual_network_link" "blob" {
  name                  = random_string.vnet_link.result
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.blob.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

resource "azurerm_private_endpoint" "stg" {
  name                          = local.stg_pve_name
  resource_group_name           = azurerm_resource_group.main.name
  location                      = local.location
  subnet_id                     = azurerm_subnet.infra.id
  custom_network_interface_name = local.stg_pve_nic_name

  private_service_connection {
    name                           = local.stg_pve_name
    is_manual_connection           = false
    private_connection_resource_id = azurerm_storage_account.main.id
    subresource_names              = ["blob"]
  }

  private_dns_zone_group {
    name = "default"
    private_dns_zone_ids = [
      azurerm_private_dns_zone.blob.id
    ]
  }
}

# TODO Containers in Storage Account

resource "azurerm_app_configuration" "main" {
  name                = "diginsight-analyzer-cfg-${var.suffix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location
  local_auth_enabled  = false
  sku                 = "standard"
}

resource "azurerm_role_assignment" "cfg_owner" {
  scope                = azurerm_app_configuration.main.id
  principal_id         = data.azuread_group.contributors.object_id
  role_definition_name = "App Configuration Data Owner"
}

resource "azurerm_role_assignment" "cfg_reader" {
  scope                = azurerm_app_configuration.main.id
  principal_id         = azuread_application.main.object_id
  role_definition_name = "App Configuration Data Reader"
}

resource "random_uuid" "cfg_nic_ipconfig" {
  keepers = {
    suffix = var.suffix
  }
}

locals {
  cfg_pve_name     = "${azurerm_app_configuration.main.name}-pve-01"
  cfg_pve_nic_name = "${local.cfg_pve_name}-nic"
}

resource "azurerm_network_interface" "cfg" {
  name                = local.cfg_pve_nic_name
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location

  ip_configuration {
    name                          = "privateEndpointIpConfig.${random_uuid.cfg_nic_ipconfig.result}"
    private_ip_address_allocation = "Dynamic"
    subnet_id                     = azurerm_subnet.infra.id
  }
}

resource "azurerm_private_dns_zone" "cfg" {
  name                = "privatelink.azconfig.io"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_dns_a_record" "cfg" {
  name                = azurerm_app_configuration.main.name
  resource_group_name = azurerm_resource_group.main.name
  zone_name           = azurerm_private_dns_zone.cfg.name
  ttl                 = 10
  records = [
    azurerm_network_interface.cfg.private_ip_address
  ]
}

resource "azurerm_private_dns_zone_virtual_network_link" "cfg" {
  name                  = random_string.vnet_link.result
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.cfg.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

resource "azurerm_private_endpoint" "cfg" {
  name                          = local.cfg_pve_name
  resource_group_name           = azurerm_resource_group.main.name
  location                      = local.location
  subnet_id                     = azurerm_subnet.infra.id
  custom_network_interface_name = local.cfg_pve_nic_name

  private_service_connection {
    name                           = local.cfg_pve_name
    is_manual_connection           = false
    private_connection_resource_id = azurerm_app_configuration.main.id
    subresource_names              = ["configurationStores"]
  }

  private_dns_zone_group {
    name = "default"
    private_dns_zone_ids = [
      azurerm_private_dns_zone.cfg.id
    ]
  }
}

# TODO Entries in App Configuration

resource "azurerm_cosmosdb_account" "main" {
  name                          = "diginsight-analyzer-cdb-${var.suffix}"
  resource_group_name           = azurerm_resource_group.main.name
  location                      = local.location
  offer_type                    = "Standard"
  local_authentication_disabled = true
  public_network_access_enabled = false
  tags = {
    "defaultExperience"       = "Core (SQL)"
    "hidden-cosmos-mmspecial" = ""
  }

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = local.location
    failover_priority = 0
  }
}

# TODO IAM of Cosmos DB

resource "random_uuid" "cdb_nic_ipconfig_pri" {
  keepers = {
    suffix = var.suffix
  }
}

resource "random_uuid" "cdb_nic_ipconfig_sec" {
  keepers = {
    suffix = var.suffix
  }
}

locals {
  cdb_pve_name     = "${azurerm_cosmosdb_account.main.name}-pve-01"
  cdb_pve_nic_name = "${local.cdb_pve_name}-nic"
}

resource "azurerm_network_interface" "cdb" {
  name                = local.cdb_pve_nic_name
  resource_group_name = azurerm_resource_group.main.name
  location            = local.location

  ip_configuration {
    name                          = "privateEndpointIpConfig.${random_uuid.cdb_nic_ipconfig_pri.result}"
    private_ip_address_allocation = "Dynamic"
    subnet_id                     = azurerm_subnet.infra.id
    primary                       = true
  }

  ip_configuration {
    name                          = "privateEndpointIpConfig.${random_uuid.cdb_nic_ipconfig_sec.result}"
    private_ip_address_allocation = "Dynamic"
    subnet_id                     = azurerm_subnet.infra.id
    primary                       = false
  }

  lifecycle {
    ignore_changes = [
      ip_configuration[0].name,
      ip_configuration[1].name
    ]
  }
}

resource "azurerm_private_dns_zone" "cdb" {
  name                = "privatelink.documents.azure.com"
  resource_group_name = azurerm_resource_group.main.name
}

resource "azurerm_private_dns_a_record" "cdb_pri" {
  name                = azurerm_cosmosdb_account.main.name
  resource_group_name = azurerm_resource_group.main.name
  zone_name           = azurerm_private_dns_zone.cdb.name
  ttl                 = 10
  records = [
    azurerm_network_interface.cdb.ip_configuration[0].private_ip_address
  ]
}

resource "azurerm_private_dns_a_record" "cdb_sec" {
  name                = "${azurerm_cosmosdb_account.main.name}-${local.location}"
  resource_group_name = azurerm_resource_group.main.name
  zone_name           = azurerm_private_dns_zone.cdb.name
  ttl                 = 10
  records = [
    azurerm_network_interface.cdb.ip_configuration[1].private_ip_address
  ]
}

resource "azurerm_private_dns_zone_virtual_network_link" "cdb" {
  name                  = random_string.vnet_link.result
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.cdb.name
  virtual_network_id    = azurerm_virtual_network.main.id
}

resource "azurerm_private_endpoint" "cdb" {
  name                          = local.cdb_pve_name
  resource_group_name           = azurerm_resource_group.main.name
  location                      = local.location
  subnet_id                     = azurerm_subnet.infra.id
  custom_network_interface_name = local.cdb_pve_nic_name

  private_service_connection {
    name                           = local.cdb_pve_name
    is_manual_connection           = false
    private_connection_resource_id = azurerm_cosmosdb_account.main.id
    subresource_names              = ["Sql"]
  }

  private_dns_zone_group {
    name = "default"
    private_dns_zone_ids = [
      azurerm_private_dns_zone.cdb.id
    ]
  }
}

# TODO Databases and collections in Cosmos DB
